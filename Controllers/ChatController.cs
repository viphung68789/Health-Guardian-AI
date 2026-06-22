
using Health_Guardian_AI.Models;
using Health_Guardian_AI.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Health_Guardian_AI.Controllers
{
    public class ChatController : Controller
    {
        private readonly AppDbContext _context;
        private readonly GeminiService _geminiService;
        private bool Flag = false;

        /// <summary>
        /// Ngưỡng nén: khi có đủ 5 cặp (10 tin nhắn), tiến hành nén memory.
        /// </summary>
        private const int CompressionPairThreshold = 10;

        public ChatController(AppDbContext context, GeminiService geminiService)
        {
            _context = context;
            _geminiService = geminiService;
        }
        private async Task<bool> IsSpamRequest(string userId)
        {
            var lastMessage = await _context.ChatMessages
                .Where(x => x.UserId == userId && x.Role == ChatRole.User)
                .OrderByDescending(x => x.Timestamp)
                .FirstOrDefaultAsync();

            if (lastMessage == null)
                return false;

            return (DateTime.Now - lastMessage.Timestamp).TotalSeconds < 10;
        }
        // ═══════════════════════════════════════════════════════════════════════
        // POST: /Chat/SendMessage
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (string.IsNullOrEmpty(request.Message))
                return BadRequest("Tin nhắn không được để trống.");
            if (await IsSpamRequest(userId))
            {
                return Json(new
                {
                    response = "Bạn gửi tin nhắn quá nhanh. Vui lòng chờ 10 giây."
                });
            }
            // ── Bước 1: Nén memory nếu đủ 5 cặp cũ ──────────────────────────
            // Thực hiện TRƯỚC khi lưu tin nhắn mới để chỉ nén các cặp hoàn chỉnh.
            try
            {
                await CompressMemoryIfNeededAsync(userId);
            }
            catch
            {
                // Nén thất bại → bỏ qua, không ảnh hưởng luồng chat chính.
                _context.ChangeTracker.Clear();
            }

            // ── Bước 2: Lưu tin nhắn người dùng ──────────────────────────────
            var userMsg = new ChatMessage
            {
                UserId = userId,
                Message = request.Message,
                Role = ChatRole.User,
                Timestamp = DateTime.Now
            };
            _context.ChatMessages.Add(userMsg);
            await _context.SaveChangesAsync();

            // ── Bước 3: Lấy dữ liệu cần thiết để xây dựng context ────────────
            var profile = await _context.HealthProfiles
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.RecordedDate)
                .FirstOrDefaultAsync();

            var userMemory = await _context.UserMemories
                .FirstOrDefaultAsync(m => m.UserId == userId);

            var recentHistory = await _context.ChatMessages
                .Where(m => m.UserId == userId)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            // ── Bước 4: Xây dựng system instruction (role + sức khỏe + memory) ─
            var systemInstruction = BuildSystemInstruction(profile, userMemory);

            // ── Bước 5: Gọi Gemini với multi-turn conversation ────────────────
            var historyForGemini = recentHistory
                .Select(m => (
                    role: m.Role == ChatRole.User ? "user" : "model",
                    message: m.Message
                ))
                .ToList();
            var aiResponse = "";
                aiResponse = await _geminiService.GetChatResponse(systemInstruction, historyForGemini);
                // ── Bước 6: Lưu phản hồi AI ──────────────────────────────────────
                var aiMsg = new ChatMessage
            {
                UserId = userId,
                Message = aiResponse,
                Role = ChatRole.Model,
                Timestamp = DateTime.Now
            };
            _context.ChatMessages.Add(aiMsg);
            await _context.SaveChangesAsync();
            return Json(new { response = aiResponse });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // GET: /Chat/GetHistory
        // ═══════════════════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> GetHistory()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var history = await _context.ChatMessages
                .Where(m => m.UserId == userId)
                .OrderBy(m => m.Timestamp)
                .Select(m => new
                {
                    role = m.Role.ToString().ToLower(),
                    message = m.Message,
                    time = m.Timestamp.ToString("HH:mm")
                })
                .ToListAsync();

            return Json(history);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PRIVATE: Logic nén memory
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Khi có ≥ 5 cặp (40 tin nhắn) trong DB:
        ///   1. Lấy 5 cặp cũ nhất.
        ///   2. Lấy Memory hiện tại của user.
        ///   3. Gửi Memory cũ + 5 cặp cho Gemini → tạo Memory mới duy nhất.
        ///   4. Update UserMemory.
        ///   5. Xóa 40 tin nhắn đã nén.
        /// </summary>
        private async Task CompressMemoryIfNeededAsync(string userId)
        {
            var totalMessages = await _context.ChatMessages
                .CountAsync(m => m.UserId == userId);

            if (totalMessages < CompressionPairThreshold * 2)
                return; // Chưa đủ 5 cặp

            // ── 1. Lấy 5 cặp cũ nhất (10 tin nhắn) ─────────────────────────
            var oldMessages = await _context.ChatMessages
                .Where(m => m.UserId == userId)
                .OrderBy(m => m.Timestamp)
                .Take(CompressionPairThreshold * 2)
                .ToListAsync();

            // ── 2. Lấy Memory hiện tại ────────────────────────────────────────
            var userMemory = await _context.UserMemories
                .FirstOrDefaultAsync(m => m.UserId == userId);

            // ── 3. Gọi Gemini tạo Memory mới ─────────────────────────────────
            var compressionPrompt = BuildCompressionPrompt(oldMessages, userMemory?.MemoryContent);
            var newMemory = await _geminiService.GetAIResponse(compressionPrompt);

            // Không nén nếu Gemini trả về lỗi
            if (string.IsNullOrWhiteSpace(newMemory)
                || newMemory.StartsWith("Lỗi")
                || newMemory.StartsWith("Xin lỗi"))
                return;

            // ── 4. Update UserMemory ──────────────────────────────────────────
            if (userMemory == null)
            {
                _context.UserMemories.Add(new UserMemory
                {
                    UserId = userId,
                    MemoryContent = newMemory,
                    UpdatedAt = DateTime.Now
                });
            }
            else
            {
                userMemory.MemoryContent = newMemory;
                userMemory.UpdatedAt = DateTime.Now;
            }

            // ── 5. Xóa 40 tin nhắn đã được nén ──────────────────────────────
            _context.ChatMessages.RemoveRange(oldMessages);

            await _context.SaveChangesAsync();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PRIVATE: Builders
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// System instruction gửi lên Gemini: vai trò AI + sức khỏe + memory tóm tắt.
        /// </summary>
        private string BuildSystemInstruction(HealthProfile? profile, UserMemory? memory)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Bạn là Bác sĩ gia đình theo dõi sức khỏe bệnh nhân.");
            sb.AppendLine("Hãy trả lời ngắn gọn, chuyên nghiệp và thân thiện bằng tiếng Việt.");

            if (profile != null)
            {
                sb.AppendLine("\n[THÔNG TIN SỨC KHỎE NGƯỜI DÙNG]");
                sb.AppendLine($"Tuổi: {profile.Age} | BMI: {profile.BMI}");
                sb.AppendLine($"Huyết áp cao: {(profile.Hypertension ? "Có" : "Không")} | " +
                              $"Bệnh tim: {(profile.HeartDisease ? "Có" : "Không")} | " +
                              $"Tiểu đường: {(profile.Diabetes ? "Có" : "Không")}");
            }

            if (memory != null && !string.IsNullOrWhiteSpace(memory.MemoryContent))
            {
                sb.AppendLine("\n[TÓM TẮT LỊCH SỬ TƯ VẤN TRƯỚC]");
                sb.AppendLine(memory.MemoryContent);
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Prompt yêu cầu Gemini tổng hợp memory cũ + 5 cặp hội thoại → memory mới duy nhất.
        /// </summary>
        private string BuildCompressionPrompt(List<ChatMessage> messages, string? existingMemory)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Bạn là hệ thống quản lý bộ nhớ của AI tư vấn y tế.");
            sb.AppendLine("Nhiệm vụ: tổng hợp thông tin thành một bản ghi nhớ duy nhất, súc tích.");
            sb.AppendLine("Yêu cầu bắt buộc: CHỈ trả về nội dung tóm tắt, không thêm lời dẫn hay giải thích.");

            if (!string.IsNullOrWhiteSpace(existingMemory))
            {
                sb.AppendLine("\n[BỘ NHỚ HIỆN TẠI CẦN TÍCH HỢP]");
                sb.AppendLine(existingMemory);
            }

            sb.AppendLine("\n[5 CẶP HỘI THOẠI CẦN TÓM TẮT]");
            foreach (var msg in messages)
            {
                var label = msg.Role == ChatRole.User ? "Người dùng" : "AI";
                sb.AppendLine($"{label}: {msg.Message}");
            }

            sb.AppendLine("\n[ĐẦU RA]");
            sb.AppendLine("Tạo bản ghi nhớ duy nhất (≤ 300 từ, tiếng Việt) kết hợp bộ nhớ cũ và hội thoại mới.");
            sb.AppendLine("Ghi lại dưới dạng từng đoạn văn: triệu chứng đã đề cập, lo lắng sức khỏe, " +
                          "lời khuyên quan trọng đã đưa ra, và các chủ đề đã thảo luận.");

            return sb.ToString();
        }
        private string pushInPrompt(HealthProfile? profile,Prediction? predict)
        {
            var pip = new StringBuilder();
            
            if (profile != null)
            {
                pip.AppendLine("\n[THÔNG TIN SỨC KHỎE NGƯỜI DÙNG]");
                pip.AppendLine($"Tuổi: {profile.Age} | BMI: {profile.BMI}");
                pip.AppendLine($"Huyết áp cao: {(profile.Hypertension ? "Có" : "Không")} | " +
                              $"Bệnh tim: {(profile.HeartDisease ? "Có" : "Không")} | " +
                              $"Tiểu đường: {(profile.Diabetes ? "Có" : "Không")}");
                pip.AppendLine($"Chẩn đoán tỉ lệ đột quỵ từ AI cá nhân: {predict.Probability} |" +
                                $"Nguyên nhân chủ yếu 1: {predict.Factors_1} |" +
                                $"Nguyễn nhân chủ yếu 2: {predict.Factors_2}");
            
            }
            pip.AppendLine("Đọc kết quả chẩn đoán khả năng chẩn đoán đột quỵ và thông số sức khỏe đưa ra lời khuyên ngắn gọn xúc tích");
            pip.AppendLine("Nêu ra những ý chính thôi.");
            return pip.ToString();
        }
        // Gửi advice cho trang
        [HttpPost]
        public async Task<IActionResult> GetHealthAdvice()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (_context.HealthProfiles.Where(o => o.UserId == userId).IsNullOrEmpty())
                return Json(new { advice = "" });
                // Lấy hồ sơ sức khỏe mới nhất
                var profile = await _context.HealthProfiles
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.RecordedDate)
                .FirstOrDefaultAsync();
            var inform = _context.InFormPredictions.Where(x => x.HPId == profile.Id).FirstOrDefault();
            if(inform != null)
                return Json(new { advice = inform.Messenge});
            if (profile == null)
                return BadRequest("Chưa có hồ sơ sức khỏe.");

            // Lấy kết quả dự đoán mới nhất
            var predict = await _context.Predictions
                .Where(p => p.HPId == profile.Id)// tuỳ tên cột trong DB của bạn
                .FirstOrDefaultAsync();

            if (predict == null)
                return BadRequest("Chưa có kết quả dự đoán.");

            // Gọi hàm buildPrompt rồi gửi lên Gemini
            var prompt = pushInPrompt(profile, predict);
            var aiResponse = await _geminiService.GetAIResponse(prompt);
            _context.InFormPredictions.Add(new InFormPrediction
            {
                HPId = profile.Id,
                Messenge = aiResponse,
            });
            await _context.SaveChangesAsync();
            return Json(new { advice = aiResponse });
        }
    }

    public class ChatRequest()
    {
        public string Message { get; set; }
    }
}
