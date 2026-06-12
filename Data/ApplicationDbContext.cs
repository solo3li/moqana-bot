using Microsoft.EntityFrameworkCore;
using TelegramAdminPanel.Models;

namespace TelegramAdminPanel.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<BotSetting> BotSettings { get; set; }
        public DbSet<BotUser> BotUsers { get; set; }
        public DbSet<BotMessage> BotMessages { get; set; }
        public DbSet<AdminUser> AdminUsers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships
            modelBuilder.Entity<BotMessage>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.ChatId)
                .HasPrincipalKey(u => u.ChatId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
