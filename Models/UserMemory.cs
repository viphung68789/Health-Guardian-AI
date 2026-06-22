namespace Health_Guardian_AI.Models
{
    public class UserMemory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; }
        public virtual User User { get; set; }

        /// <summary>
        /// Nội dung tóm tắt các hội thoại cũ đã bị xóa, do Gemini tự sinh.
        /// </summary>
        public string MemoryContent { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
