using AIStudyHub.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
        });

        b.Entity<Document>(e =>
        {
            e.Property(d => d.Tags).HasColumnType("text[]").HasDefaultValueSql("'{}'");
            e.HasOne(d => d.User).WithMany(u => u.Documents).HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Subject).WithMany(s => s.Documents).HasForeignKey(d => d.SubjectId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Subject>(e =>
        {
            e.HasOne(s => s.User).WithMany(u => u.Subjects).HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Conversation>(e =>
        {
            e.HasOne(c => c.User).WithMany(u => u.Conversations).HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Message>(e =>
        {
            e.HasOne(m => m.Conversation).WithMany(c => c.Messages).HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
