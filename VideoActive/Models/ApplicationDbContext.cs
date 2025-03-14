using Microsoft.EntityFrameworkCore;
using VideoActive.Models;

namespace VideoActive.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<Relationship> Relationships { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Chatbox> Chatboxes { get; set; }
        public DbSet<CallLog> CallLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Unique constraints
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Admin>()
                .HasIndex(a => a.Username)
                .IsUnique();

            // Relationship Constraints
            modelBuilder.Entity<Relationship>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Relationship>()
                .HasOne(r => r.Friend)
                .WithMany()
                .HasForeignKey(r => r.FriendId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Chatbox>()
                .HasOne(c => c.User1)
                .WithMany()
                .HasForeignKey(c => c.UserId1)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Chatbox>()
                .HasOne(c => c.User2)
                .WithMany()
                .HasForeignKey(c => c.UserId2)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CallLog>()
                .HasIndex(c => c.CID)
                .IsUnique();
                
        }
    }
}
