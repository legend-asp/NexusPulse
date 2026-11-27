namespace NexusPulse.Infrastructure.BackgroundJobs;

using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexusPulse.Application.Common.Interfaces;
using NexusPulse.Application.Common.Models;
using NexusPulse.Domain.Enums;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class TelegramPollingWorker : BackgroundService
{
    private readonly ITelegramBotClient? _botClient;
    private readonly IIncidentChannel _incidentChannel;
    private readonly IMetricsBroadcaster _broadcaster;
    private readonly IMonitoredNodeRegistry _nodeRegistry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramPollingWorker> _logger;
    private readonly string? _authorizedChatId;

    public TelegramPollingWorker(
        IConfiguration configuration,
        IIncidentChannel incidentChannel,
        IMetricsBroadcaster broadcaster,
        IMonitoredNodeRegistry nodeRegistry,
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramPollingWorker> logger)
    {
        _incidentChannel = incidentChannel;
        _broadcaster = broadcaster;
        _nodeRegistry = nodeRegistry;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _authorizedChatId = configuration["TelegramSettings:ChatId"];
        var token = configuration["TelegramSettings:BotToken"];

        if (!string.IsNullOrWhiteSpace(token) && !token.Contains("YOUR_TELEGRAM"))
        {
            _botClient = new TelegramBotClient(token);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_botClient == null)
        {
            _logger.LogWarning("Telegram bot token not configured. Polling disabled.");
            return;
        }

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.CallbackQuery, UpdateType.Message }
        };

        _logger.LogInformation("TelegramPollingWorker is listening for messages and button callbacks...");

        await _botClient.ReceiveAsync(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken
        );
    }

    private bool IsAuthorizedUser(long chatId)
    {
        if (string.IsNullOrWhiteSpace(_authorizedChatId) || _authorizedChatId.Contains("YOUR_TELEGRAM"))
            return true;

        return string.Equals(chatId.ToString(), _authorizedChatId.Trim(), StringComparison.Ordinal);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        try
        {
            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                var chatId = update.Message.Chat.Id;
                if (!IsAuthorizedUser(chatId))
                {
                    _logger.LogWarning("Ignored Telegram message from unauthorized ChatId {ChatId}", chatId);
                    return;
                }

                var text = update.Message.Text.Trim();
                _logger.LogInformation("Received Telegram command: {Command} from {ChatId}", text, chatId);

                if (text.StartsWith("/status", StringComparison.OrdinalIgnoreCase))
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: BuildClusterStatusMessage(),
                        parseMode: ParseMode.Html,
                        cancellationToken: ct);
                    return;
                }

                var welcome = "⚡ <b>NexusPulse SRE Incident Dispatcher</b>\n\n" +
                              "🟢 <b>Status:</b> Active &amp; Alerting-Only\n" +
                              $"📍 <b>Connected Chat:</b> <code>{chatId}</code>\n\n" +
                              "Команды:\n" +
                              "• /status — текущая сводка по всем нодам кластера\n" +
                              "• /start — меню управления\n";

                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("📊 Cluster Health", "test:cluster_status"),
                        InlineKeyboardButton.WithCallbackData("🔥 Test Alert", "test:trigger_outage")
                    }
                });

                await botClient.SendMessage(
                    chatId: chatId,
                    text: welcome,
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard,
                    cancellationToken: ct);

                return;
            }

            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
            {
                var callback = update.CallbackQuery;
                if (!IsAuthorizedUser(callback.From.Id))
                {
                    await botClient.AnswerCallbackQuery(callback.Id, "⛔ Access denied.", showAlert: true, cancellationToken: ct);
                    return;
                }

                var data = callback.Data ?? string.Empty;
                _logger.LogInformation("Received Telegram callback: {Data} from user {UserId}", data, callback.From.Id);

                if (data == "test:trigger_outage")
                {
                    var firstNode = _nodeRegistry.GetNodes().FirstOrDefault();
                    var nodeId = firstNode?.Id ?? Guid.Parse("f47ac10b-58cc-4372-a567-0e02b2c3d479");
                    var (nodeName, _, displayUrl) = _nodeRegistry.GetNodeInfo(nodeId);

                    await botClient.AnswerCallbackQuery(
                        callbackQueryId: callback.Id,
                        text: $"🔥 Тестовый алерт отправлен для {nodeName}",
                        showAlert: false,
                        cancellationToken: ct);

                    await _incidentChannel.PublishAsync(new IncidentDetectedEvent(
                        Guid.NewGuid(),
                        nodeId,
                        nodeName,
                        displayUrl,
                        "Manual test probe failure: HTTP 504 Gateway Timeout after 5000ms",
                        "HttpTimeout",
                        504,
                        DateTime.UtcNow
                    ), ct);
                }
                else if (data == "test:cluster_status")
                {
                    var nodes = _nodeRegistry.GetNodes();
                    var healthy = nodes.Count(n => n.HealthStatus == NodeStatus.Healthy);
                    await botClient.AnswerCallbackQuery(
                        callbackQueryId: callback.Id,
                        text: $"📊 Healthy: {healthy}/{nodes.Count} нод в работе.",
                        showAlert: true,
                        cancellationToken: ct);

                    if (callback.Message != null)
                    {
                        await botClient.SendMessage(
                            chatId: callback.Message.Chat.Id,
                            text: BuildClusterStatusMessage(),
                            parseMode: ParseMode.Html,
                            cancellationToken: ct);
                    }
                }
                else if (data.StartsWith("restart:", StringComparison.Ordinal) || data.Contains("restart", StringComparison.Ordinal))
                {
                    var parts = data.Split(':');
                    var nodeIdStr = parts.Length > 1 ? parts[1] : string.Empty;
                    if (!Guid.TryParse(nodeIdStr, out var nodeId) || nodeId == Guid.Empty)
                    {
                        nodeId = _nodeRegistry.GetNodes().FirstOrDefault()?.Id ?? Guid.Parse("f47ac10b-58cc-4372-a567-0e02b2c3d479");
                    }

                    var (nodeName, _, displayUrl) = _nodeRegistry.GetNodeInfo(nodeId);

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
                        await cache.ReleaseSimulationLockAsync(nodeId);
                        await cache.ReleaseIncidentLockAsync(nodeId);

                        var notifier = scope.ServiceProvider.GetRequiredService<ITelegramNotifier>();
                        await notifier.SendRecoveryAlertAsync(nodeName, displayUrl, TimeSpan.Zero);
                    }

                    _nodeRegistry.RecordSuccess(nodeId, 24);
                    await _broadcaster.BroadcastRecoveryAsync(nodeId, nodeName);

                    await botClient.AnswerCallbackQuery(
                        callbackQueryId: callback.Id,
                        text: $"✅ Инцидент для {nodeName} сброшен! Запущена перепроверка.",
                        showAlert: false,
                        cancellationToken: ct);
                }
                else if (data.StartsWith("mute:", StringComparison.Ordinal))
                {
                    var parts = data.Split(':');
                    if (parts.Length > 1 && Guid.TryParse(parts[1], out var nodeId))
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
                        await cache.ReleaseIncidentLockAsync(nodeId);
                        await cache.TryAcquireIncidentLockAsync(nodeId, Guid.NewGuid(), TimeSpan.FromHours(1));
                    }

                    await botClient.AnswerCallbackQuery(
                        callbackQueryId: callback.Id,
                        text: "🔕 Повторные алерты по этой ноде подавлены на 1 час.",
                        showAlert: true,
                        cancellationToken: ct);
                }
                else if (data.StartsWith("details:", StringComparison.Ordinal))
                {
                    await botClient.AnswerCallbackQuery(
                        callbackQueryId: callback.Id,
                        text: "🔍 Полный Postmortem доступен в веб-дашборде.",
                        showAlert: false,
                        cancellationToken: ct);
                }
                else
                {
                    await botClient.AnswerCallbackQuery(callback.Id, "Команда принята.", cancellationToken: ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Telegram update.");
        }
    }

    private string BuildClusterStatusMessage()
    {
        var nodes = _nodeRegistry.GetNodes();
        var healthyCount = nodes.Count(n => n.HealthStatus == NodeStatus.Healthy);
        var sb = new StringBuilder();
        sb.AppendLine($"📊 <b>Cluster Status ({healthyCount}/{nodes.Count} Healthy)</b>\n");

        foreach (var node in nodes)
        {
            var icon = node.HealthStatus == NodeStatus.Healthy ? "🟢"
                : node.HealthStatus == NodeStatus.Degraded ? "🟡" : "🔴";
            var lat = node.HealthStatus == NodeStatus.Down
                ? "DOWN"
                : $"{_nodeRegistry.GetLastLatency(node.Id)} ms";
            sb.AppendLine($"{icon} <b>{WebUtility.HtmlEncode(node.Name)}</b> — <code>{lat}</code>");
        }

        return sb.ToString();
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Telegram Polling error occurred.");
        return Task.CompletedTask;
    }
}
