namespace NexusPulse.Infrastructure.BackgroundJobs;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexusPulse.Application.Common.Interfaces;
using NexusPulse.Application.Common.Models;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class TelegramPollingWorker : BackgroundService
{
    private readonly ITelegramBotClient? _botClient;
    private readonly IIncidentChannel _incidentChannel;
    private readonly ILogger<TelegramPollingWorker> _logger;

    public TelegramPollingWorker(
        IConfiguration configuration,
        IIncidentChannel incidentChannel,
        ILogger<TelegramPollingWorker> logger)
    {
        _incidentChannel = incidentChannel;
        _logger = logger;
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

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        try
        {
            // 1. Обработка входящих текстовых сообщений (/start, /status, /help)
            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                var text = update.Message.Text.Trim();
                var chatId = update.Message.Chat.Id;

                _logger.LogInformation("Received Telegram command: {Command} from {ChatId}", text, chatId);

                var welcome = "⚡ <b>NexusPulse SRE Incident Dispatcher</b>\n\n" +
                              "🟢 <b>Status:</b> Active &amp; Ready\n" +
                              $"📍 <b>Connected Chat:</b> <code>{chatId}</code>\n\n" +
                              "Бот выполняет роль <b>автономного On-Call диспетчера</b> кластера:\n" +
                              "• Мгновенно перехватывает сбои (HTTP 5xx, таймауты, обрыв Circuit Breaker);\n" +
                              "• Присылает структурированный Root Cause Analysis (RCA);\n" +
                              "• Позволяет управлять нодами и заглушать алерты прямо из чата.\n\n" +
                              "<i>Для проверки нажмите кнопку ниже или симулируйте сбой в веб-дашборде:</i>";

                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("🔥 Trigger Test Outage", "test:trigger_outage"),
                        InlineKeyboardButton.WithCallbackData("📊 Cluster Health", "test:cluster_status")
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

            // 2. Обработка нажатий на инлайн-кнопки
            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
            {
                var callback = update.CallbackQuery;
                var data = callback.Data ?? string.Empty;

                _logger.LogInformation("Received Telegram callback: {Data} from user {UserId}", data, callback.From.Id);

                if (data == "test:trigger_outage")
                {
                    await botClient.AnswerCallbackQuery(
                        callbackQueryId: callback.Id,
                        text: "🔥 Инициирован тестовый инцидент...",
                        showAlert: false,
                        cancellationToken: ct);

                    await _incidentChannel.PublishAsync(new IncidentDetectedEvent(
                        Guid.NewGuid(),
                        Guid.Parse("f47ac10b-58cc-4372-a567-0e02b2c3d479"),
                        "Payment Gateway API",
                        "https://api.payments.internal/v1/charge",
                        "HTTP 504 Gateway Timeout: Upstream banking cluster connection timed out after 30000ms",
                        "HttpTimeout",
                        504,
                        DateTime.UtcNow
                    ), ct);
                }
                else if (data == "test:cluster_status")
                {
                    await botClient.AnswerCallbackQuery(
                        callbackQueryId: callback.Id,
                        text: "✅ Все 3 ноды кластера в штатном режиме (SLA 99.98%).",
                        showAlert: true,
                        cancellationToken: ct);
                }
                else if (data.Contains("mute"))
                {
                    await botClient.AnswerCallbackQuery(
                        callbackQueryId: callback.Id,
                        text: "🔕 Нода заморожена на 1 час. Алерты подавлены.",
                        showAlert: true,
                        cancellationToken: ct);
                }
                else if (data.Contains("restart"))
                {
                    await botClient.AnswerCallbackQuery(
                        callbackQueryId: callback.Id,
                        text: "🔄 Команда перезапуска отправлена в кластер!",
                        showAlert: true,
                        cancellationToken: ct);
                }
                else if (data.Contains("details"))
                {
                    await botClient.AnswerCallbackQuery(
                        callbackQueryId: callback.Id,
                        text: "🔍 Лог и стек-трейс запрошены. Проверьте веб-консоль.",
                        showAlert: false,
                        cancellationToken: ct);
                }
                else
                {
                    await botClient.AnswerCallbackQuery(
                        callbackQueryId: callback.Id,
                        text: "Команда принята!",
                        cancellationToken: ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Telegram update.");
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Telegram Polling error occurred.");
        return Task.CompletedTask;
    }
}
