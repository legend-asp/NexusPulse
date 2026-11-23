namespace NexusPulse.UnitTests.Services;

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using NexusPulse.Application.Common.Models;
using NexusPulse.Infrastructure.Services;
using Xunit;

public class GeminiDiagnosticServiceTests
{
    [Fact]
    public async Task DiagnoseIncidentAsync_WhenGeminiReturnsValidJson_ShouldParseSuccessfully()
    {
        // Arrange
        var mockResponseJson = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new
                            {
                                text = JsonSerializer.Serialize(new
                                {
                                    rootCause = "Database deadlock during high-concurrency payment capture.",
                                    immediateFix = "Kill blocking PostgreSQL PID and restart connection pool.",
                                    prevention = "Optimize foreign key indexes and wrap capture in serializable isolation."
                                })
                            }
                        }
                    }
                }
            }
        });

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(mockResponseJson)
            });

        var httpClient = new HttpClient(handlerMock.Object);

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("GeminiClient")).Returns(httpClient);

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["GeminiSettings:ApiKey"]).Returns("AIzaSyTestApiKey");
        configMock.Setup(c => c["GeminiSettings:Model"]).Returns("gemini-3.1-flash-lite");

        var loggerMock = new Mock<ILogger<GeminiDiagnosticService>>();

        var service = new GeminiDiagnosticService(factoryMock.Object, configMock.Object, loggerMock.Object);

        var incidentEvent = new IncidentDetectedEvent(
            IncidentId: Guid.NewGuid(),
            NodeId: Guid.NewGuid(),
            NodeName: "Payment Gateway",
            TargetUrl: "https://pay.internal/charge",
            ErrorMessage: "504 Gateway Timeout",
            FailureType: "HttpTimeout",
            StatusCode: 504,
            OccurredAt: DateTime.UtcNow
        );

        // Act
        var diagnosis = await service.DiagnoseIncidentAsync(incidentEvent);

        // Assert
        diagnosis.Should().NotBeNull();
        diagnosis.RootCause.Should().Contain("Database deadlock");
        diagnosis.ImmediateFix.Should().Contain("Kill blocking PostgreSQL PID");
        diagnosis.Prevention.Should().Contain("Optimize foreign key indexes");
    }

    [Fact]
    public async Task DiagnoseIncidentAsync_WhenApiKeyMissing_ShouldReturnFallbackDiagnosis()
    {
        // Arrange
        var factoryMock = new Mock<IHttpClientFactory>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["GeminiSettings:ApiKey"]).Returns(string.Empty); // Missing key
        var loggerMock = new Mock<ILogger<GeminiDiagnosticService>>();

        var service = new GeminiDiagnosticService(factoryMock.Object, configMock.Object, loggerMock.Object);

        var incidentEvent = new IncidentDetectedEvent(
            IncidentId: Guid.NewGuid(),
            NodeId: Guid.NewGuid(),
            NodeName: "Auth Service",
            TargetUrl: "https://auth.internal/health",
            ErrorMessage: "Connection refused",
            FailureType: "ConnectionRefused",
            StatusCode: 500,
            OccurredAt: DateTime.UtcNow
        );

        // Act
        var diagnosis = await service.DiagnoseIncidentAsync(incidentEvent);

        // Assert: Never throws, returns safe fallback
        diagnosis.Should().NotBeNull();
        diagnosis.RootCause.Should().NotBeNullOrWhiteSpace();
        diagnosis.ImmediateFix.Should().NotBeNullOrWhiteSpace();
        diagnosis.Prevention.Should().NotBeNullOrWhiteSpace();
    }
}
