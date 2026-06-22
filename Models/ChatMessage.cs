namespace Health_Guardian_AI.Models
{
    public enum ChatRole
    {
        User,Model
    }
    public class ChatMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ChatRole Role { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public string UserId { get; set; }
        public virtual User User { get; set; }

    }
}
