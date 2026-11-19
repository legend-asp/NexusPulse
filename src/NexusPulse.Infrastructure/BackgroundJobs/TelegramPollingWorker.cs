namespace NexusPulse.Infrastructure.BackgroundJobs;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

public class TelegramPollingWorker : BackgroundService
{
    private readonly ITelegramBotClient? _botClient;
    private readonly ILogger<TelegramPollingWorker> _logger;

    public TelegramPollingWorker(IConfiguration configuration, ILogger<TelegramPollingWorker> logger)
    {
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

        _logger.LogInformation("TelegramPollingWorker is listening for button clicks...");

        await _botClient.ReceiveAsync(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken
        );
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        if (update.Type != UpdateType.CallbackQuery || update.CallbackQuery == null)
            return;

        var callback = update.CallbackQuery;
        var data = callback.Data ?? string.Empty;

        _logger.LogInformation("Received Telegram callback: {Data} from user {UserId}", data, callback.From.Id);

        if (data.Contains("mute"))
        {
            await botClient.AnswerCallbackQuery(
                callbackQueryId: callback.Id,
                text: "🔕 Нода успешно заморожена на 1 час!",
                showAlert: true,
                cancellationToken: ct);
        }
        else if (data.Contains("restart"))
        {
            await botClient.AnswerCallbackQuery(
                callbackQueryId: callback.Id,
                text: "🔄 Сигнал перезапуска отправлен в оркестратор!",
                showAlert: true,
                cancellationToken: ct);
        }
        else if (data.Contains("details"))
        {
            await botClient.AnswerCallbackQuery(
                callbackQueryId: callback.Id,
                text: "🔍 Лог и стек-трейс запрошены. Проверьте консоль мониторинга.",
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

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Telegram Polling error occurred.");
        return Task.CompletedTask;
    }
}
