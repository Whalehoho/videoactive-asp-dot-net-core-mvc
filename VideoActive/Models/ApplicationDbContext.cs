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
        public DbSet<Relationship> Relationships { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Chatbox> Chatboxes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Unique constraints
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
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
        }
    }
}
