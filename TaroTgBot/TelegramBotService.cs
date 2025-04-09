using TaroTgBot.Core;
using Telegram.Bot;
using Telegram.Bot.Types;

public class TelegramBotService
{
    private readonly ITelegramBotClient _botClient;
    private readonly CommandHandler _commandHandler;
    private readonly CallbackHandler _callbackHandler;

    public TelegramBotService(TelegramBotClient botClient, Dictionary<long, string> userInputs, TaskQueue taskQueue, FirebaseService firebaseService)
    {
        _botClient = botClient;
        _commandHandler = new CommandHandler(botClient, userInputs, taskQueue, firebaseService);
        _callbackHandler = new CallbackHandler(botClient, userInputs);
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message?.SuccessfulPayment != null)
        {
            await _commandHandler.ProcessSuccessfulPaymentAsync(update.Message, cancellationToken);
            return;  // Завершаем обработку, чтобы избежать "Неизвестная команда"
        }

        if (update.Message != null)
        {
            await _commandHandler.HandleCommandAsync(update.Message, cancellationToken);
        }
        else if (update.CallbackQuery != null)
        {
            await _callbackHandler.HandleCallbackAsync(update.CallbackQuery, cancellationToken);
        }
        else if (update.PreCheckoutQuery != null)
        {
            await _commandHandler.ProcessPreCheckoutQueryAsync(update.PreCheckoutQuery, cancellationToken);
        }
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Ошибка: {exception.Message}");
        return Task.CompletedTask;
    }
}