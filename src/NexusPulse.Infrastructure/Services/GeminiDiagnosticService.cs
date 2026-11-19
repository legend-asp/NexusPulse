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
        _apiKey = configuration["GeminiSettings:ApiKey"];
        _model = configuration["GeminiSettings:Model"] ?? "gemini-3.1-flash-lite";
        _logger = logger;
    }

    public async Task<AiDiagnosis> DiagnoseIncidentAsync(IncidentDetectedEvent incidentEvent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey.Contains("YOUR_GEMINI"))
        {
            _logger.LogWarning("Gemini API key is not configured. Returning fallback diagnosis.");
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

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
            var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gemini API error (Status {StatusCode}): {Error}", response.StatusCode, errorText);
                return GetFallbackDiagnosis(incidentEvent);
            }

            var responseJson = await response.Content.ReadFromJsonAsync<GeminiApiResponse>(cancellationToken: cancellationToken);
            var text = responseJson?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Gemini returned empty candidate text. Using fallback.");
                return GetFallbackDiagnosis(incidentEvent);
            }

            var diagnosis = JsonSerializer.Deserialize<AiDiagnosis>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return diagnosis ?? GetFallbackDiagnosis(incidentEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to diagnose incident via Gemini API. Falling back to local heuristics.");
            return GetFallbackDiagnosis(incidentEvent);
        }
    }

    private static AiDiagnosis GetFallbackDiagnosis(IncidentDetectedEvent incidentEvent)
    {
        return new AiDiagnosis(
            RootCause: $"Service '{incidentEvent.NodeName}' failed with {incidentEvent.FailureType}: {incidentEvent.ErrorMessage}",
            ImmediateFix: "Check host connectivity, inspect container logs, and verify network ingress.",
            Prevention: "Configure automated health checks, scale replica count, and ensure proper timeouts."
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
