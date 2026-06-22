using Microsoft.EntityFrameworkCore;
namespace Health_Guardian_AI.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Ánh xạ các Model thành các bảng tương ứng trong SQL Server
        public DbSet<User> Users { get; set; }
        public DbSet<HealthProfile> HealthProfiles { get; set; }
        public DbSet<Prediction> Predictions { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<UserMemory> UserMemories { get; set; }
        public DbSet<InFormPrediction> InFormPredictions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Ép kiểu hiển thị cột Enum ChatRole thành dạng String chữ thay vì số Int trong Database (tùy chọn)
            modelBuilder.Entity<ChatMessage>()
                .Property(c => c.Role)
                .HasConversion<string>();

            // User (1) -> (N) HealthProfile
            modelBuilder.Entity<HealthProfile>()
                .HasOne(h => h.User)
                .WithMany(u => u.HealthProfiles)
                .HasForeignKey(h => h.UserId);

            // HealthProfile (1) -> (1) Prediction
            modelBuilder.Entity<Prediction>()
                .HasOne(p => p.HealthProfile)
                .WithOne(h => h.Prediction)
                .HasForeignKey<Prediction>(p => p.HPId);
            // ChatMessage (1) -> (1) User
            modelBuilder.Entity<ChatMessage>()
                .HasOne(c => c.User)
                .WithMany(c => c.ChatMessages)
                .HasForeignKey(u => u.UserId);
            // HealtProfile (1) -> (1) InFormPrediction
            modelBuilder.Entity<InFormPrediction>()
                .HasOne(p => p.HealthProfile)
                .WithOne(p => p.InFormPrediction)
                .HasForeignKey<InFormPrediction>(u => u.HPId);
        }
    }
}
