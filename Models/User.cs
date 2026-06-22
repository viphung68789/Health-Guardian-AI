using System.ComponentModel.DataAnnotations;
namespace Health_Guardian_AI.Models
{
    public class User
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Role { get; set; } = "Patient"; //Doctor | Patient;
        public virtual ICollection<HealthProfile> HealthProfiles { get; set; } = new List<HealthProfile>();
        public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

        public virtual ICollection<UserMemory> UserMemories { get; set; } = new List<UserMemory>();
    }
}
