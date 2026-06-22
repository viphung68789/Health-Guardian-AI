using System.Text;
using System.Text.Json;

namespace Health_Guardian_AI.Service
{
    public class GeminiService
    {
        private readonly string _apiKey = "";
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "";
        private readonly MarkdownService _md;
        private readonly GeminiService _gemini;

        public GeminiService(HttpClient httpClient, MarkdownService md)
        {
            _httpClient = httpClient;
            _md = md;
        }

        /// <summary>
        /// Dùng cho prompt đơn lẻ (nén memory, tóm tắt, v.v.)
        /// </summary>
        public async Task<string> GetAIResponse(string prompt)
        {
            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                }
            };

            return await CallGeminiAsync(requestBody);
        }

        /// <summary>
        /// Dùng cho hội thoại nhiều lượt (chat), truyền system instruction và toàn bộ history.
        /// history: danh sách (role "user"/"model", nội dung tin nhắn)
        /// </summary>
        public async Task<string> GetChatResponse(
            string systemInstruction,
            List<(string role, string message)> history)
        {
            var requestBody = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemInstruction } }
                },
                contents = history.Select(h => new
                {
                    role = h.role,
                    parts = new[] { new { text = h.message } }
                }).ToArray()
            };

            return await CallGeminiAsync(requestBody);
        }

        // ─── Private helper ──────────────────────────────────────────────────────

        private async Task<string> CallGeminiAsync(object requestBody)
        {
            var url = $"{BaseUrl}?key={_apiKey}";
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                // Đọc body một lần duy nhất
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return $"<p class='text-red-500'>Lỗi API ({(int)response.StatusCode})</p>";

                using var doc = JsonDocument.Parse(responseBody);

                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();
                if (string.IsNullOrEmpty(text))
                    return "<p>Xin lỗi, tôi không thể xử lý câu hỏi này.</p>";
                return _md.ToHtml(text);
            }
            catch (Exception ex)
            {
                return $"Lỗi kết nối AI: {ex.Message}";
            }
        }
    }
}
