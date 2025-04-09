using System.Text;
using System.Text.Json;

namespace TaroTgBot.Services
{
    public class RequestGpt
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;

        // Конструктор теперь называется RequestGpt, как и имя класса
        public RequestGpt(string apiKey = "hyMihVzHyAOppU7X2KFueiBuvRE8xPkj", string model = "mistral-large-latest")
        {
            _apiKey = apiKey;
            _model = model;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.mistral.ai/v1/")
            };
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<string> RunGptTest(string prompt)
        {
            try
            {
                var request = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.7,
                    max_tokens = 2048,
                    stream = false
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                
                string result = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString()
                    .Trim();
                // Создаем случайное число задержки в миллисекундах от 7 до 12 минут.
                var random = new Random();
                int delayMilliseconds = random.Next(6 * 60 * 1000, 12 * 60 * 1000);
                // Задержка перед отправкой результата пользователю
                await Task.Delay(delayMilliseconds);
                return result;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"API Error: {ex.StatusCode} - {ex.Message}");
                return "Ошибка подключения к сервису. Пожалуйста, попробуйте позже.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General error: {ex.Message}");
                return "Произошла ошибка при обработке вашего запроса. Попробуйте еще раз.";
            }
        }
    }
}