using AIStudyHub.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Api.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public virtual DbSet<ChatMessage> ChatMessages { get; set; }
    public virtual DbSet<ChatSession> ChatSessions { get; set; }
    public virtual DbSet<CloudFile> CloudFiles { get; set; }
    public virtual DbSet<Document> Documents { get; set; }
    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
    public virtual DbSet<StoragePackage> StoragePackages { get; set; }
    public virtual DbSet<Subject> Subjects { get; set; }
    public virtual DbSet<Tag> Tags { get; set; }
    public virtual DbSet<Transaction> Transactions { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<UserStorage> UserStorages { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Favorite> Favorites { get; set; }

    public virtual DbSet<SubscriptionPackage> SubscriptionPackages { get; set; }

    public virtual DbSet<UserSubscription> UserSubscriptions { get; set; }

    public virtual DbSet<UserSession> UserSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ai_study_hub");

        // doc_visibility, cloud_status, chat_role, payment_method, payment_status, purchase_type:
        // bo khoi danh sach Postgres enum native - cac cot tuong ung gio dung varchar +
        // HasConversion<string> (xem cau hinh tung entity ben duoi).
        modelBuilder
            .HasPostgresEnum<UserRole>("ai_study_hub", "user_role");

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("chat_messages_pkey");
            entity.ToTable("chat_messages", "ai_study_hub");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            // Doi sang varchar + HasConversion<string>: cung loi enum native Postgres nhu Document.Visibility.
            entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(20).HasConversion<string>();
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.TokensUsed).HasColumnName("tokens_used").HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.SessionId).HasColumnName("session_id");

            entity.HasOne(d => d.Session).WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("chat_messages_session_id_fkey");
        });

        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("chat_sessions_pkey");
            entity.ToTable("chat_sessions", "ai_study_hub");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.Title).HasMaxLength(255).HasColumnName("title");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.ChatSessions)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("chat_sessions_user_id_fkey");

            entity.HasMany(d => d.Documents).WithMany(p => p.Sessions)
                .UsingEntity<Dictionary<string, object>>(
                    "ChatDocumentContext",
                    r => r.HasOne<Document>().WithMany()
                        .HasForeignKey("DocumentId")
                        .HasConstraintName("chat_document_context_document_id_fkey"),
                    l => l.HasOne<ChatSession>().WithMany()
                        .HasForeignKey("SessionId")
                        .HasConstraintName("chat_document_context_session_id_fkey"),
                    j =>
                    {
                        j.HasKey("SessionId", "DocumentId").HasName("chat_document_context_pkey");
                        j.ToTable("chat_document_context", "ai_study_hub");
                        j.IndexerProperty<Guid>("SessionId").HasColumnName("session_id");
                        j.IndexerProperty<Guid>("DocumentId").HasColumnName("document_id");
                    });
        });

        modelBuilder.Entity<CloudFile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("cloud_files_pkey");
            entity.ToTable("cloud_files", "ai_study_hub");
            entity.HasIndex(e => e.DocumentId, "cloud_files_document_id_key").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.CloudKey).HasMaxLength(300).HasColumnName("cloud_key");
            entity.Property(e => e.CloudUrl).HasMaxLength(500).HasColumnName("cloud_url");
            // Doi sang varchar + HasConversion<string>: cung loi enum native Postgres nhu Document.Visibility.
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20)
                .HasConversion<string>().HasDefaultValue(CloudStatus.pending);
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.Provider).HasMaxLength(20).HasColumnName("provider");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("now()").HasColumnName("uploaded_at");

            entity.HasOne(d => d.Document).WithOne(p => p.CloudFile)
                .HasForeignKey<CloudFile>(d => d.DocumentId)
                .HasConstraintName("cloud_files_document_id_fkey");
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("documents_pkey");
            entity.ToTable("documents", "ai_study_hub");
            entity.HasIndex(e => e.SubjectId, "idx_documents_subject_id");
            entity.HasIndex(e => e.UserId, "idx_documents_user_id");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.FilePath).HasMaxLength(500).HasColumnName("file_path");
            entity.Property(e => e.FileSize).HasColumnName("file_size");
            entity.Property(e => e.FileType).HasMaxLength(20).HasColumnName("file_type");
            // Cot visibility doi tu enum native Postgres sang varchar: Npgsql/EF Core ban hien tai
            // doc/viet sai kieu (int) cho cot enum native, gay loi 42883 khi query va loi doc du
            // lieu khi insert. Dung varchar + HasConversion<string> de tranh hoan toan bug nay.
            entity.Property(e => e.Visibility).HasColumnName("visibility").HasMaxLength(20)
                .HasConversion<string>().HasDefaultValue(DocVisibility.@public);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false).HasColumnName("is_deleted");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");
            entity.Property(e => e.Title).HasMaxLength(255).HasColumnName("title");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Subject).WithMany(p => p.Documents)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("documents_subject_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Documents)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("documents_user_id_fkey");

            entity.HasMany(d => d.Tags).WithMany(p => p.Documents)
                .UsingEntity<Dictionary<string, object>>(
                    "DocumentTag",
                    r => r.HasOne<Tag>().WithMany()
                        .HasForeignKey("TagId")
                        .HasConstraintName("document_tags_tag_id_fkey"),
                    l => l.HasOne<Document>().WithMany()
                        .HasForeignKey("DocumentId")
                        .HasConstraintName("document_tags_document_id_fkey"),
                    j =>
                    {
                        j.HasKey("DocumentId", "TagId").HasName("document_tags_pkey");
                        j.ToTable("document_tags", "ai_study_hub");
                        j.IndexerProperty<Guid>("DocumentId").HasColumnName("document_id");
                        j.IndexerProperty<Guid>("TagId").HasColumnName("tag_id");
                    });
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("password_reset_tokens_pkey");
            entity.ToTable("password_reset_tokens", "ai_study_hub");
            entity.HasIndex(e => e.UserId, "idx_password_tokens_user_id");
            entity.HasIndex(e => e.Token, "password_reset_tokens_token_key").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.IsUsed).HasDefaultValue(false).HasColumnName("is_used");
            entity.Property(e => e.Token).HasMaxLength(255).HasColumnName("token");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.PasswordResetTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("password_reset_tokens_user_id_fkey");
        });

        modelBuilder.Entity<StoragePackage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("storage_packages_pkey");
            entity.ToTable("storage_packages", "ai_study_hub");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.CapacityBytes).HasColumnName("capacity_bytes");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.Name).HasMaxLength(100).HasColumnName("name");
            entity.Property(e => e.Price).HasPrecision(12, 2).HasColumnName("price");
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("subjects_pkey");
            entity.ToTable("subjects", "ai_study_hub");
            entity.HasIndex(e => e.Code, "subjects_code_key").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.Code).HasMaxLength(50).HasColumnName("code");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name).HasMaxLength(255).HasColumnName("name");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tags_pkey");
            entity.ToTable("tags", "ai_study_hub");
            entity.HasIndex(e => e.Name, "tags_name_key").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.Name).HasMaxLength(100).HasColumnName("name");
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("transactions_pkey");
            entity.ToTable("transactions", "ai_study_hub");
            entity.HasIndex(e => e.UserId, "idx_transactions_user_id");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.Amount).HasPrecision(12, 2).HasColumnName("amount");
            // Doi sang varchar + HasConversion<string>: cung loi enum native Postgres nhu Document.Visibility,
            // chuan bi san cho chuc nang Storage & Payment sau nay (chua co controller nao dung toi).
            entity.Property(e => e.Method).HasColumnName("method").HasMaxLength(20).HasConversion<string>();
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.PackageId).HasColumnName("package_id");
            entity.Property(e => e.SubscriptionPackageId).HasColumnName("subscription_package_id");
            entity.Property(e => e.PurchaseKind).HasColumnName("purchase_kind").HasMaxLength(20).HasConversion<string>();
            entity.Property(e => e.StorageAddedBytes)
                .HasComment("Bí quyết linh hoạt: Khách mua gói 10GB hay nhập tay 3.5GB thì Backend chỉ việc quy ra Bytes ném vào đây. Hóa đơn completed là Trigger số 3 tự bốc số này cộng thẳng vào ví storage.")
                .HasColumnName("storage_added_bytes");
            entity.Property(e => e.TransactionRef)
                .HasMaxLength(255)
                .HasColumnName("transaction_ref");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Package).WithMany(p => p.Transactions)
                .HasForeignKey(d => d.PackageId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("transactions_package_id_fkey");

            entity.HasOne(d => d.SubscriptionPackage).WithMany(p => p.Transactions)
                .HasForeignKey(d => d.SubscriptionPackageId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("transactions_subscription_package_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Transactions)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("transactions_user_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");
            entity.ToTable("users", "ai_study_hub");
            entity.HasIndex(e => e.Username, "users_username_key").IsUnique();
            entity.HasIndex(e => e.Email, "idx_users_email_lower").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasComment("Dùng VARCHAR(255) kết hợp UNIQUE INDEX LOWER() để ép hệ thống hiểu \"Email@gmail\" và \"email@gmail\" là một, chống tạo 2 tài khoản trùng lặp. Đã bỏ CITEXT để tránh lỗi phân quyền trên Cloud.")
                .HasColumnName("email");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasComment("Nguyên tắc tử huyệt: Không bao giờ lưu mật khẩu gốc (plaintext). Cột này lưu chuỗi đã mã hóa một chiều (Bcrypt/Argon2).")
                .HasColumnName("password_hash");
            entity.Property(e => e.RoleId)
                .HasColumnName("role_id");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("users_role_id_fkey");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .HasColumnName("username");

            entity.Property(e => e.TwoFactorEnabled)
                .HasDefaultValue(false)
                .HasColumnName("two_factor_enabled");

            entity.Property(e => e.TwoFactorSecret)
                .HasMaxLength(255)
                .HasColumnName("two_factor_secret");

            entity.Property(e => e.TwoFactorPendingSecret)
                .HasMaxLength(255)
                .HasColumnName("two_factor_pending_secret");
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_sessions_pkey");
            entity.ToTable("user_sessions", "ai_study_hub");
            entity.HasIndex(e => e.UserId, "idx_user_sessions_user_id");
            entity.HasIndex(e => e.RefreshTokenHash, "user_sessions_refresh_token_hash_key").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RefreshTokenHash).HasMaxLength(255).HasColumnName("refresh_token_hash");
            entity.Property(e => e.DeviceName).HasMaxLength(255).HasColumnName("device_name");
            entity.Property(e => e.UserAgent).HasMaxLength(500).HasColumnName("user_agent");
            entity.Property(e => e.IpAddress).HasMaxLength(64).HasColumnName("ip_address");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.LastActiveAt).HasDefaultValueSql("now()").HasColumnName("last_active_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");

            entity.HasOne(d => d.User).WithMany(p => p.Sessions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_sessions_user_id_fkey");
        });

        modelBuilder.Entity<UserStorage>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("user_storage_pkey");
            entity.ToTable("user_storage", "ai_study_hub");

            entity.Property(e => e.UserId).ValueGeneratedNever().HasColumnName("user_id");
            entity.Property(e => e.TotalCapacityBytes).HasDefaultValue(10485760L).HasColumnName("total_capacity_bytes");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
            entity.Property(e => e.UsedBytes).HasDefaultValue(0L).HasColumnName("used_bytes");

            entity.HasOne(d => d.User).WithOne(p => p.UserStorage)
                .HasForeignKey<UserStorage>(d => d.UserId)
                .HasConstraintName("user_storage_user_id_fkey");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pkey");
            entity.ToTable("roles", "ai_study_hub");
            entity.HasIndex(e => e.Name, "roles_name_key").IsUnique();
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.Name).HasMaxLength(50).HasColumnName("name");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        });

        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.DocumentId }).HasName("favorites_pkey");
            entity.ToTable("favorites", "ai_study_hub");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

            entity.HasOne(d => d.User).WithMany(p => p.Favorites)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("favorites_user_id_fkey");

            entity.HasOne(d => d.Document).WithMany(p => p.Favorites)
                .HasForeignKey(d => d.DocumentId)
                .HasConstraintName("favorites_document_id_fkey");
        });

        modelBuilder.Entity<SubscriptionPackage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("subscription_packages_pkey");
            entity.ToTable("subscription_packages", "ai_study_hub");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.Name).HasMaxLength(100).HasColumnName("name");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Price).HasPrecision(12, 2).HasColumnName("price");
            entity.Property(e => e.DurationDays).HasColumnName("duration_days");
            entity.Property(e => e.AiChatLimit).HasDefaultValue(100).HasColumnName("ai_chat_limit");
            entity.Property(e => e.BaseStorageBytes).HasDefaultValue(10485760L).HasColumnName("base_storage_bytes"); // 10 MB (demo)
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
        });

        modelBuilder.Entity<UserSubscription>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_subscriptions_pkey");
            entity.ToTable("user_subscriptions", "ai_study_hub");
            entity.HasIndex(e => e.UserId, "idx_user_subscriptions_user");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.PackageId).HasColumnName("package_id");
            entity.Property(e => e.StartDate).HasDefaultValueSql("now()").HasColumnName("start_date");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("active").HasColumnName("status");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");

            entity.HasOne(d => d.User).WithMany(p => p.UserSubscriptions)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("user_subscriptions_user_id_fkey");

            entity.HasOne(d => d.Package).WithMany(p => p.UserSubscriptions)
                .HasForeignKey(d => d.PackageId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("user_subscriptions_package_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
