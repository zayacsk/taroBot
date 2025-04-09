using Telegram.Bot;
using Telegram.Bot.Types;

public class CallbackHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly Dictionary<long, string> _userInputs; // Общий словарь для хранения состояний пользователей

    public CallbackHandler(ITelegramBotClient botClient, Dictionary<long, string> userInputs)
    {
        _botClient = botClient;
        _userInputs = userInputs;
    }

    public async Task HandleCallbackAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data == "request_spread")
        {
            // Устанавливаем состояние пользователя как ожидающее ввода
            _userInputs[callbackQuery.Message!.Chat.Id] = "awaiting_input";

            await _botClient.SendTextMessageAsync(
                chatId: callbackQuery.Message.Chat.Id,
                text: "Пожалуйста, укажите ваше имя, возраст и сферу жизни, на которую хотите получить расклад (например, \"Карьеру\" или \"Любовь\").",
                cancellationToken: cancellationToken
            );
        }
    }
}