using TaroTgBot.Core;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

var cts = new CancellationTokenSource();
var botClient = new TelegramBotClient("7777935189:AAHQ_RHmO_t9Kz_qjbpRQpRAj4u6JbHxkZg");

var userInputs = new Dictionary<long, string>();
var taskQueue = new TaskQueue();
var firebaseService = new FirebaseService("https://tgbotpayment-default-rtdb.firebaseio.com/", "Pn0rL82UBXXUWlsPp88Zajkcc8OBW8ccoKxkGuYk");
var botService = new TelegramBotService(botClient, userInputs, taskQueue, firebaseService);
var queueProcessor = new QueueProcessor(taskQueue, cts.Token);

_ = Task.Run(() => queueProcessor.StartProcessingAsync());

// Обновленная подписка: теперь делегат StartReceiving принимает (ITelegramBotClient, Update, CancellationToken)
botClient.StartReceiving(
    async (client, update, token) =>
    {
        // При каждом новом сообщении сбрасываем таймер напоминания
        if (update.Message != null)
        {
            string userId = update.Message.Chat.Id.ToString();
            string username = update.Message.Chat.Username;
            // Если потребуется, можно извлечь номер телефона
            string phoneNumber = null;
            await firebaseService.UpdateLastReminderDate(userId, username, phoneNumber, DateTime.UtcNow);
        }

        // Теперь HandleUpdateAsync вызывается с тремя параметрами
        await botService.HandleUpdateAsync(client, update, token);
    },
    async (client, exception, token) =>
    {
        await botService.HandleErrorAsync(client, exception, token);
    },
    cancellationToken: cts.Token
);

Console.WriteLine($"Bot @{(await botClient.GetMeAsync()).Username} is running...");

// Таймер для еженедельных напоминаний (проверка раз в 24 часа)
var timer = new Timer(async _ =>
{
    try
    {
        var users = await firebaseService.GetAllUsersAsync();
        foreach (var user in users)
        {
            // Проверяем, прошло ли 10 дней с момента последнего напоминания
            if (user.LastReminderDate == default(DateTime) || DateTime.UtcNow - user.LastReminderDate >= TimeSpan.FromDays(10))
            {
                try
                {
                    await botClient.SendTextMessageAsync(
                        chatId: long.Parse(user.UserId),
                        text: "🔮 Привет! Напоминаем, что наши тарологи всегда рядом, чтобы помочь тебе заглянуть в будущее. Всего за 50 ⭐️ ты получаешь детальный расклад. Мы напоминаем о возможности воспользоваться ботом, чтобы ты не упустил шанс узнать ответы на важные вопросы. Попробуй уже сегодня и начни своё путешествие к самопознанию!",
                        cancellationToken: cts.Token
                    );

                    // Обновляем дату последнего напоминания
                    await firebaseService.UpdateLastReminderDate(
                        user.UserId,
                        user.Username,
                        user.PhoneNumber,
                        DateTime.UtcNow
                    );
                }
                catch (ApiRequestException apiEx) when (apiEx.ErrorCode == 403 && apiEx.Message.Contains("bot was blocked by the user"))
                {
                    //Console.WriteLine($"Пользователь {user.UserId} - {user.Username} заблокировал бота. Напоминание не отправлено.");
                    // Здесь можно добавить логику для удаления пользователя из базы данных или пометить его как неактивного
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при отправке напоминания пользователю {user.UserId}: {ex.Message}");
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Общая ошибка при отправке напоминаний: {ex.Message}");
    }
},
null,
TimeSpan.Zero,
TimeSpan.FromHours(24)); // Проверка каждые 24 часа


Console.ReadLine();
cts.Cancel();