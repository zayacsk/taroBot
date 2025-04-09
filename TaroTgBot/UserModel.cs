public class UserModel
{
    public string UserId { get; set; }
    public string Username { get; set; } // Может быть null, если сохраняется телефон
    public string PhoneNumber { get; set; } // Может быть null, если сохраняется username
    public float Balance { get; set; }
    public DateTime LastReminderDate { get; set; } // Поле для даты последнего напоминания
}