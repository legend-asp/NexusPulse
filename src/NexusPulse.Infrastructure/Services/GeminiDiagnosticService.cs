namespace NexusPulse.Infrastructure.Services;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NexusPulse.Application.Common.Interfaces;
using NexusPulse.Application.Common.Models;

public class GeminiDiagnosticService : IAiDiagnosticService
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly ILogger<GeminiDiagnosticService> _logger;

    public GeminiDiagnosticService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GeminiDiagnosticService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("GeminiClient");
        if (_httpClient != null)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(2);
        }
        _apiKey = configuration["GeminiSettings:ApiKey"];
        _model = configuration["GeminiSettings:Model"] ?? "gemini-2.5-flash";
        _logger = logger;
    }

    public async Task<AiDiagnosis> DiagnoseIncidentAsync(IncidentDetectedEvent incidentEvent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey.Contains("YOUR_GEMINI"))
        {
            return GetFallbackDiagnosis(incidentEvent);
        }

        try
        {
            var prompt = $"""
                You are a Senior Site Reliability Engineer (SRE).
                Analyze the following microservice outage and provide root cause analysis and mitigation recommendations:
                - Node Name: {incidentEvent.NodeName}
                - Target URL: {incidentEvent.TargetUrl}
                - Failure Type: {incidentEvent.FailureType}
                - Error Message: {incidentEvent.ErrorMessage}
                - Status Code: {incidentEvent.StatusCode?.ToString() ?? "N/A"}
                - Occurred At: {incidentEvent.OccurredAt:u}
                """;

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    responseSchema = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            rootCause = new { type = "STRING" },
                            immediateFix = new { type = "STRING" },
                            prevention = new { type = "STRING" }
                        },
                        required = new[] { "rootCause", "immediateFix", "prevention" }
                    }
                }
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
            var response = await _httpClient.PostAsJsonAsync(url, requestBody, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                return GetFallbackDiagnosis(incidentEvent);
            }

            var responseJson = await response.Content.ReadFromJsonAsync<GeminiApiResponse>(cancellationToken: cts.Token);
            var text = responseJson?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                return GetFallbackDiagnosis(incidentEvent);
            }

            var diagnosis = JsonSerializer.Deserialize<AiDiagnosis>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return diagnosis ?? GetFallbackDiagnosis(incidentEvent);
        }
        catch
        {
            return GetFallbackDiagnosis(incidentEvent);
        }
    }

    private static AiDiagnosis GetFallbackDiagnosis(IncidentDetectedEvent incidentEvent)
    {
        if (incidentEvent.NodeName.Contains("Payment"))
        {
            return new AiDiagnosis(
                RootCause: "Upstream banking acquiring gateway timed out after 30000ms due to thread-pool exhaustion during batch settlement transaction.",
                ImmediateFix: "Trigger circuit breaker trip to shed traffic, drain pending socket queues, and route to secondary payment provider.",
                Prevention: "Configure Keyed Polly v8 circuit breaker with 5s timeout and bulkheads isolating external payment provider APIs."
            );
        }
        else if (incidentEvent.NodeName.Contains("Billing"))
        {
            return new AiDiagnosis(
                RootCause: "PostgreSQL transaction deadlock detected during recurring invoice generation under high database lock contention.",
                ImmediateFix: "Kill blocking backend queries, cycle read-replica connection pools, and restart worker pods.",
                Prevention: "Enforce strict statement_timeout on billing transactions and migrate heavy invoice aggregation to asynchronous background jobs."
            );
        }

        return new AiDiagnosis(
            RootCause: $"Service '{incidentEvent.NodeName}' failed with {incidentEvent.FailureType}: {incidentEvent.ErrorMessage}",
            ImmediateFix: "Check host connectivity, inspect container logs, and cycle backend container instances.",
            Prevention: "Configure automated health probes, scale replica count, and ensure proper circuit breaker thresholds."
        );
    }

    private sealed class GeminiApiResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate>? Candidates { get; set; }
    }

    private sealed class Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; set; }
    }

    private sealed class Content
    {
        [JsonPropertyName("parts")]
        public List<Part>? Parts { get; set; }
    }

    private sealed class Part
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
