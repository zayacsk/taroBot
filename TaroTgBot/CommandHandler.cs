using TaroTgBot.Core;
using TaroTgBot.Handlers;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Payments;
using Telegram.Bot.Types.ReplyMarkups;

public class CommandHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly Dictionary<long, string> _userInputs;
    private readonly TarotHandler _tarotHandler;
    private readonly NumerologyHandler _numerologyHandler;
    private readonly TaskQueue _taskQueue;
    private readonly FirebaseService _firebaseService;

    public CommandHandler(ITelegramBotClient botClient, Dictionary<long, string> userInputs, TaskQueue taskQueue, FirebaseService firebaseService)
    {
        _botClient = botClient;
        _userInputs = userInputs;
        _taskQueue = taskQueue;
        _firebaseService = firebaseService;
        _tarotHandler = new TarotHandler();
        _numerologyHandler = new NumerologyHandler();
    }

    public async Task HandleCommandAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        
        if (_userInputs.TryGetValue(message.Chat.Id, out var state))
        {
            if (state == "awaiting_tarot_input" || state == "awaiting_numerology_input")
            {
                if (message.Text == "🔙 Назад")
                {
                    await _firebaseService.AddOrUpdateUser(chatId.ToString(), message.Chat.Username ?? "Unknown",message.Contact?.PhoneNumber ?? "UnknownPhone", 1);
                    
                    // Удаляем состояние и возвращаемся в главное меню
                    _userInputs.Remove(message.Chat.Id);
                    await SendMainMenuAsync(message.Chat.Id, cancellationToken);
                    return;
                }

                // Убираем клавиатуру перед обработкой запроса
                var removeKeyboard = new ReplyKeyboardRemove();

                await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Спасибо! Я записал ваш запрос, он уже в очереди. Скоро вы получите ответ.✨",
                    replyMarkup: removeKeyboard,
                    cancellationToken: cancellationToken
                );

                // Добавляем задачу в очередь
                _taskQueue.Enqueue(async () =>
                {
                    if (state == "awaiting_tarot_input")
                    {
                        // Обработка запроса Таро
                        string gptResponse = await _tarotHandler.ProcessTarotRequest(message.Text); // Добавляем await

                        // Отправляем результат
                        await _botClient.SendTextMessageAsync(
                            chatId: message.Chat.Id,
                            text: $"{gptResponse}",
                            cancellationToken: cancellationToken
                        );
                    }
                    else if (state == "awaiting_numerology_input")
                    {
                        // Обработка запроса Нумерологии
                        string gptResponse = await _numerologyHandler.ProcessNumerologyRequest(message.Text); // Добавляем await

                        // Отправляем результат
                        await _botClient.SendTextMessageAsync(
                            chatId: message.Chat.Id,
                            text: $"{gptResponse}",
                            cancellationToken: cancellationToken
                        );
                    }

                    // Удаляем состояние и возвращаемся в главное меню
                    _userInputs.Remove(message.Chat.Id);
                    await SendMainMenuAsync(message.Chat.Id, cancellationToken);
                });
                return;
            }
        }

        switch (message.Text)
        {
            case "/start":
                var startBalance = await _firebaseService.GetUserBalance(chatId.ToString(), message.Chat.Username ?? "Unknown", message.Contact?.PhoneNumber);
                await SendMainMenuAsync(message.Chat.Id, cancellationToken);
                break;
            
            case "💰 Баланс":
                var balance = await _firebaseService.GetUserBalance(chatId.ToString(), message.Chat.Username ?? "Unknown", message.Contact?.PhoneNumber);
                await _botClient.SendTextMessageAsync(chatId, $"💰 У вас на балансе {balance} оплаченных раскладов. Ваш id чата: {chatId}", cancellationToken: cancellationToken);
                break;

            case "💳 Оплата":
                await _botClient.SendInvoiceAsync(chatId,
                    title: "Расклад Таро",
                    description: "Персональный расклад Таро для глубокого понимания ситуации и возможных путей.",
                    payload: "tarot_reading",
                    currency: "XTR",
                    prices: new[]
                    {
                        new Telegram.Bot.Types.Payments.LabeledPrice("Расклад Таро", 50)
                    },
                    photoUrl: "https://icon-icons.com/icons2/1286/PNG/128/36_85248.png",
                    cancellationToken: cancellationToken
                );
                break;
            
            case "📃 Техническая поддержка":
                await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Если возникли вопросы или что-то пошло не так, пишите @TaroBotHelp."
                );
                break;

            case "🔮 Таро":
                var tarotBalance = await _firebaseService.GetUserBalance(chatId.ToString(), message.Chat.Username ?? "Unknown", message.Contact?.PhoneNumber ?? "UnknownPhone");
                if (tarotBalance < 1)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: "🔔 На вашем балансе недостаточно средств для расклада. Один расклад стоит 100 звезд. Пополните баланс, и я с радостью сделаю вам предсказание!🔮✨",
                        cancellationToken: cancellationToken
                    );
                    return;
                }

                // Списываем 1 с баланса
                await _firebaseService.AddOrUpdateUser(chatId.ToString(), message.Chat.Username ?? "Unknown", message.Contact?.PhoneNumber ?? "UnknownPhone", -1);
                _userInputs[message.Chat.Id] = "awaiting_tarot_input";

                var tarotBackButtonKeyboard = new ReplyKeyboardMarkup(new[]
                {
                    new[] { new KeyboardButton("🔙 Назад") }
                })
                {
                    ResizeKeyboard = true,
                    OneTimeKeyboard = true
                };

                await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "🔮 Напишите ваше имя, возраст и сферу жизни, которая вас волнует (например, любовь или карьера). Если хотите — добавьте детали, это поможет точности расклада! \nПримерное время ожидания 5 - 30 минут",
                    replyMarkup: tarotBackButtonKeyboard,
                    cancellationToken: cancellationToken
                );
                break;

            case "🔢 Нумерология":
                var numerologyBalance = await _firebaseService.GetUserBalance(chatId.ToString(), message.Chat.Username ?? "Unknown", message.Contact?.PhoneNumber ?? "UnknownPhone");
                if (numerologyBalance < 1)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: "🔔 У вас недостаточно звезд для анализа нумерологии. Один расчет стоит 100 звезд. Пополните баланс, и я помогу вам расшифровать ваши числа! 🔢✨",
                        cancellationToken: cancellationToken
                    );
                    return;
                }

                // Списываем 1 с баланса
                await _firebaseService.AddOrUpdateUser(chatId.ToString(), message.Chat.Username ?? "Unknown", message.Contact?.PhoneNumber ?? "UnknownPhone", -1);
                _userInputs[message.Chat.Id] = "awaiting_numerology_input";

                var numerologyBackButtonKeyboard = new ReplyKeyboardMarkup(new[]
                {
                    new[] { new KeyboardButton("🔙 Назад") }
                })
                {
                    ResizeKeyboard = true,
                    OneTimeKeyboard = true
                };

                await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "🔢 Напишите имя и дату рождения (ДД.ММ.ГГГГ). Если хотите — добавьте детали (например, хочу узнать про карьеру), и я расскажу, что говорят числа! \nПримерное время ожидания 5 - 30 минут",
                    replyMarkup: numerologyBackButtonKeyboard,
                    cancellationToken: cancellationToken
                );
                break;

            default:
                await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Что-то пошло не так. Попробуйте выбрать один из пунктов меню или введите /start, чтобы начать сначала.",
                    cancellationToken: cancellationToken
                );
                break;
        }
    }

    private async Task SendMainMenuAsync(long chatId, CancellationToken cancellationToken)
    {
        var mainMenuKeyboard = new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("💳 Оплата"), new KeyboardButton("💰 Баланс") },
            new[] { new KeyboardButton("🔮 Таро"), new KeyboardButton("🔢 Нумерология") },
            new[] { new KeyboardButton("📃 Техническая поддержка") }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: $"Приветствую тебя! \nЯ помогу заглянуть в тайны судьбы и чисел. Выбери, что тебе интересно: \n🔮 Таро — получи персональный расклад и разберись в своих переживаниях, планах и судьбоносных моментах. \n🔢 Нумерология — узнай, что скрывают твои числа, и раскрой глубинные аспекты своей личности и будущего. \n💰 Баланс — проверь, сколько у тебя оплаченных раскладов. \n💳 Оплата — пополни баланс и открой новые возможности для предсказаний. \n📃 Техническая поддержка — если что-то не так, я подскажу, куда обратиться. \nПримерное время ожидания 5 - 30 минут \nВаш id {chatId}",
            replyMarkup: mainMenuKeyboard,
            cancellationToken: cancellationToken
        );
    }
    
    public async Task ProcessPreCheckoutQueryAsync(PreCheckoutQuery preCheckoutQuery, CancellationToken cancellationToken)
    {
        if (preCheckoutQuery.InvoicePayload == "tarot_reading")
        {
            await _botClient.AnswerPreCheckoutQueryAsync(preCheckoutQuery.Id, cancellationToken: cancellationToken);
        }
        else
        {
            await _botClient.AnswerPreCheckoutQueryAsync(preCheckoutQuery.Id, "Некорректный заказ.", cancellationToken: cancellationToken);
        }
    }

    public async Task ProcessSuccessfulPaymentAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var successfulPayment = message.SuccessfulPayment;

        Console.WriteLine($"[INFO] Successful payment received. Payload: {successfulPayment?.InvoicePayload}, Amount: {successfulPayment?.TotalAmount}");

        if (successfulPayment?.InvoicePayload == "tarot_reading")
        {
            await _firebaseService.AddOrUpdateUser(chatId.ToString(), message.Chat.Username ?? "Unknown", message.Contact?.PhoneNumber ?? "UnknownPhone", 1);
            await _botClient.SendTextMessageAsync(chatId, "Спасибо за оплату! Ваш баланс успешно пополнен, и теперь вы можете получить новые предсказания. Загляните в тайны Таро или откройте магию чисел – выбирайте, что вам ближе!🔮", cancellationToken: cancellationToken);
        }
        else
        {
            Console.WriteLine($"[ERROR] Payment payload mismatch: {successfulPayment?.InvoicePayload}");
        }
    }
}