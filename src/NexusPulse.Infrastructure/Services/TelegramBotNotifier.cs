namespace NexusPulse.Infrastructure.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NexusPulse.Application.Common.Interfaces;
using NexusPulse.Application.Common.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class TelegramBotNotifier : ITelegramNotifier
{
    private readonly ITelegramBotClient? _botClient;
    private readonly string? _chatId;
    private readonly ILogger<TelegramBotNotifier> _logger;

    public TelegramBotNotifier(IConfiguration configuration, ILogger<TelegramBotNotifier> logger)
    {
        _logger = logger;
        var token = configuration["TelegramSettings:BotToken"];
        _chatId = configuration["TelegramSettings:ChatId"];

        if (!string.IsNullOrWhiteSpace(token) && !token.Contains("YOUR_TELEGRAM"))
        {
            _botClient = new TelegramBotClient(token);
        }
        else
        {
            _logger.LogWarning("Telegram Bot token is not configured. Alerts will be logged to console only.");
        }
    }

    public async Task SendIncidentAlertAsync(IncidentDetectedEvent incidentEvent, AiDiagnosis? diagnosis = null)
    {
        if (_botClient == null || string.IsNullOrWhiteSpace(_chatId))
        {
            _logger.LogWarning("Telegram Bot is not fully configured (missing token or ChatId). Skipping alert dispatch.");
            return;
        }

        string message = $"🚨 <b>[INCIDENT ALERT] Node Down</b>\n\n" +
                         $"<b>Node:</b> {incidentEvent.NodeName}\n" +
                         $"<b>Target:</b> {incidentEvent.TargetUrl}\n" +
                         $"<b>Type:</b> {incidentEvent.FailureType}\n" +
                         $"<b>Status:</b> {incidentEvent.StatusCode?.ToString() ?? "N/A"}\n" +
                         $"<b>Error:</b> {incidentEvent.ErrorMessage}\n" +
                         $"<b>Time:</b> {incidentEvent.OccurredAt:yyyy-MM-dd HH:mm:ss} UTC\n\n";

        if (diagnosis != null)
        {
            message += $"🧠 <b>AI Diagnosis (Google Gemini):</b>\n" +
                       $"• <b>Root Cause:</b> {diagnosis.RootCause}\n" +
                       $"• <b>Immediate Fix:</b> {diagnosis.ImmediateFix}\n" +
                       $"• <b>Prevention:</b> {diagnosis.Prevention}\n";
        }

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔍 Details", $"details:{incidentEvent.IncidentId}"),
                InlineKeyboardButton.WithCallbackData("🔄 Restart Node", $"restart:{incidentEvent.NodeId}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔕 Mute 1h", $"mute:{incidentEvent.NodeId}")
            }
        });

        try
        {
            await _botClient.SendMessage(
                chatId: _chatId,
                text: message,
                parseMode: ParseMode.Html,
                replyMarkup: inlineKeyboard);
            _logger.LogInformation("Incident alert dispatched to Telegram chat {ChatId}", _chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send incident alert to Telegram");
        }
    }

    public async Task SendRecoveryAlertAsync(string nodeName, string targetUrl, TimeSpan downtime)
    {
        if (_botClient == null || string.IsNullOrWhiteSpace(_chatId))
            return;

        string message = $"✅ <b>[RECOVERED] Node Restored</b>\n\n" +
                         $"<b>Node:</b> {nodeName}\n" +
                         $"<b>Target:</b> {targetUrl}\n" +
                         $"<b>Downtime:</b> {downtime.TotalMinutes:F1} min ({downtime.TotalSeconds:F0} sec)\n" +
                         $"<b>Time:</b> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n\n" +
                         $"All health checks are passing normally.";

        try
        {
            await _botClient.SendMessage(
                chatId: _chatId,
                text: message,
                parseMode: ParseMode.Html);
            _logger.LogInformation("Recovery alert dispatched to Telegram chat {ChatId}", _chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send recovery alert to Telegram");
        }
    }
}
