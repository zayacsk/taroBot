using Firebase.Database;
using Firebase.Database.Query;

public class FirebaseService
{
    private readonly FirebaseClient _firebaseClient;

    public FirebaseService(string firebaseUrl, string firebaseSecret)
    {
        _firebaseClient = new FirebaseClient(
            firebaseUrl,
            new FirebaseOptions
            {
                AuthTokenAsyncFactory = () => Task.FromResult(firebaseSecret)
            });
    }

    public async Task AddOrUpdateUser(string userId, string username, string phoneNumber, float amount)
    {
        // Сначала пробуем найти по новому ключу (userId)
        var (user, oldKey) = await FindUserAnywhereAsync(userId, username);
        var currentBalance = user?.Balance ?? 0;

        // Если нашли по старому ключу - мигрируем данные
        if (user != null && oldKey != null)
        {
            await MigrateUserDataAsync(user, oldKey, userId);
        }

        Console.WriteLine($"[INFO] Updating user {userId} - {username}. Current balance: {currentBalance}, Adding: {amount}");

        await _firebaseClient
            .Child("users")
            .Child(userId)
            .PutAsync(new UserModel
            {
                UserId = userId,
                Username = username,
                PhoneNumber = phoneNumber,
                Balance = currentBalance + amount,
                LastReminderDate = DateTime.UtcNow
            });
    }

    public async Task<float> GetUserBalance(string userId, string username, string phoneNumber)
    {
        // Пытаемся найти пользователя по новому ключу
        var (user, oldKey) = await FindUserAnywhereAsync(userId, username);
        
        if (user == null)
        {
            Console.WriteLine($"[INFO] User {userId} not found. Creating new entry.");
            
            var newUser = new UserModel
            {
                UserId = userId,
                Username = username,
                PhoneNumber = phoneNumber,
                Balance = 1,
                LastReminderDate = DateTime.UtcNow
            };
            
            await _firebaseClient
                .Child("users")
                .Child(userId)
                .PutAsync(newUser);
            
            return 1;
        }

        // Если нашли по старому ключу - мигрируем
        if (oldKey != null)
        {
            await MigrateUserDataAsync(user, oldKey, userId);
        }

        return user.Balance;
    }
    
    private async Task<(UserModel user, string oldKey)> FindUserAnywhereAsync(string userId, string username)
    {
        // Сначала проверяем по новому ключу
        var userByNewKey = await _firebaseClient
            .Child("users")
            .Child(userId)
            .OnceSingleAsync<UserModel>();

        if (userByNewKey != null) return (userByNewKey, null);

        // Если не нашли - проверяем по старому ключу (username)
        if (!string.IsNullOrEmpty(username))
        {
            var userByOldKey = await _firebaseClient
                .Child("users")
                .Child(username)
                .OnceSingleAsync<UserModel>();

            if (userByOldKey != null) return (userByOldKey, username);
        }

        return (null, null);
    }

    private async Task MigrateUserDataAsync(UserModel user, string oldKey, string newKey)
    {
        Console.WriteLine($"[MIGRATION] Migrating user from {oldKey} to {newKey}");
        
        // Создаем новую запись
        await _firebaseClient
            .Child("users")
            .Child(newKey)
            .PutAsync(user);

        // Удаляем старую запись
        await _firebaseClient
            .Child("users")
            .Child(oldKey)
            .DeleteAsync();
    }

    public async Task<List<UserModel>> GetAllUsersAsync()
    {
        var users = await _firebaseClient
            .Child("users")
            .OnceAsync<UserModel>();
        return users.Select(u => u.Object).ToList();
    }

    public async Task UpdateLastReminderDate(string userId, string username, string phoneNumber, DateTime date)
    {
        var user = await _firebaseClient
            .Child("users")
            .Child(userId) // Обновление по userId
            .OnceSingleAsync<UserModel>();

        if (user != null)
        {
            user.LastReminderDate = date;
            await _firebaseClient
                .Child("users")
                .Child(userId)
                .PutAsync(user);
        }
    }
}