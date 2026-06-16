using System;
using System.Collections.Generic;
using AIStudyHub.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Api.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

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


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("chat_messages_pkey");

            entity.ToTable("chat_messages", "ai_study_hub");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Role)
                .HasColumnName("role");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.SessionId).HasColumnName("session_id");

            entity.HasOne(d => d.Session).WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("chat_messages_session_id_fkey");
        });

        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("chat_sessions_pkey");

            entity.ToTable("chat_sessions", "ai_study_hub");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
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
                        j.ToTable("chat_document_context", "ai_study_hub", tb => tb.HasComment("Bảng mấu chốt của AI RAG (Chat với tài liệu). Lưu liên kết giữa phiên chat và file PDF. Backend nhìn vào đây để lấy nội dung file kẹp vào Prompt gửi cho AI, giúp AI có \"phao cứu sinh\" trả lời chuẩn xác, không bịa đặt thông tin."));
                        j.IndexerProperty<Guid>("SessionId").HasColumnName("session_id");
                        j.IndexerProperty<Guid>("DocumentId").HasColumnName("document_id");
                    });
        });

        modelBuilder.Entity<CloudFile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("cloud_files_pkey");

            entity.ToTable("cloud_files", "ai_study_hub", tb => tb.HasComment("Lưu link S3/MinIO thực tế. Tách riêng bảng này để lỡ mai mốt chê AWS đắt, đổi sang Google Cloud thì chỉ sửa ở đây, không ảnh hưởng logic Documents."));

            entity.HasIndex(e => e.DocumentId, "cloud_files_document_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CloudKey)
                .HasMaxLength(300)
                .HasColumnName("cloud_key");
            entity.Property(e => e.CloudUrl)
                .HasMaxLength(500)
                .HasColumnName("cloud_url");
            entity.Property(e => e.Status)
                .HasColumnName("status");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.Provider)
                .HasMaxLength(20)
                .HasColumnName("provider");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("uploaded_at");

            entity.HasOne(d => d.Document).WithOne(p => p.CloudFile)
                .HasForeignKey<CloudFile>(d => d.DocumentId)
                .HasConstraintName("cloud_files_document_id_fkey");
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("documents_pkey");

            entity.ToTable("documents", "ai_study_hub", tb => tb.HasComment("Trái tim của hệ thống. Áp dụng cơ chế ON DELETE CASCADE từ bảng Users: Xóa user là tự động quét sạch tài liệu, không lo rác DB."));

            entity.HasIndex(e => e.SubjectId, "idx_documents_subject_id");

            entity.HasIndex(e => e.UserId, "idx_documents_user_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.FilePath)
                .HasMaxLength(500)
                .HasColumnName("file_path");
            entity.Property(e => e.FileSize)
                .HasComment("Bắt buộc dùng BIGINT để lưu số Bytes. Nếu dùng INT bình thường, file >2GB sẽ bị tràn bộ nhớ (overflow) gây sập hệ thống.")
                .HasColumnName("file_size");
            entity.Property(e => e.FileType)
                .HasMaxLength(20)
                .HasColumnName("file_type");
            entity.Property(e => e.Visibility)
                .HasColumnName("visibility");
            entity.Property(e => e.IsDeleted)
                .HasComment("Cờ Xóa mềm (Soft Delete). Đổi thành TRUE thì file chui vào thùng rác, giữ lại được 30 ngày để khôi phục thay vì bốc hơi vĩnh viễn.")
                .HasColumnName("is_deleted");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
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

            entity.ToTable("password_reset_tokens", "ai_study_hub", tb => tb.HasComment("Quản lý token quên mật khẩu. Có cột expires_at (hạn dùng) và is_used (đã dùng) để vô hiệu hóa link cũ, chống hack."));

            entity.HasIndex(e => e.UserId, "idx_password_tokens_user_id");

            entity.HasIndex(e => e.Token, "password_reset_tokens_token_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.IsUsed).HasColumnName("is_used");
            entity.Property(e => e.Token)
                .HasMaxLength(255)
                .HasColumnName("token");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.PasswordResetTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("password_reset_tokens_user_id_fkey");
        });

        modelBuilder.Entity<StoragePackage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("storage_packages_pkey");

            entity.ToTable("storage_packages", "ai_study_hub");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CapacityBytes).HasColumnName("capacity_bytes");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Price)
                .HasPrecision(12, 2)
                .HasColumnName("price");
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("subjects_pkey");

            entity.ToTable("subjects", "ai_study_hub");

            entity.HasIndex(e => e.Code, "subjects_code_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tags_pkey");

            entity.ToTable("tags", "ai_study_hub");

            entity.HasIndex(e => e.Name, "tags_name_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("transactions_pkey");

            entity.ToTable("transactions", "ai_study_hub", tb => tb.HasComment("Lưu lịch sử nạp tiền. Chìa khóa của mô hình Mua bao nhiêu dùng bấy nhiêu."));

            entity.HasIndex(e => e.UserId, "idx_transactions_user_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasPrecision(12, 2)
                .HasColumnName("amount");
            entity.Property(e => e.Method)
                .HasColumnName("method");
            entity.Property(e => e.Status)
                .HasColumnName("status");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.PackageId).HasColumnName("package_id");
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

            entity.HasOne(d => d.User).WithMany(p => p.Transactions)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("transactions_user_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users", "ai_study_hub", tb => tb.HasComment("Lưu thông tin cốt lõi. Dùng UUID thay vì ID (1,2,3) để bảo mật, chống đối thủ đoán số lượng user và dễ scale server."));

            entity.HasIndex(e => e.Username, "users_username_key").IsUnique();

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
            entity.Property(e => e.Role)
                .HasColumnName("role");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .HasColumnName("username");

            entity.Property(e => e.RefreshToken)
                .HasMaxLength(255)
                .HasColumnName("refresh_token");

            entity.Property(e => e.RefreshTokenExpiry)
                .HasColumnName("refresh_token_expiry");
        });

        modelBuilder.Entity<UserStorage>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("user_storage_pkey");

            entity.ToTable("user_storage", "ai_study_hub", tb => tb.HasComment("Ví dung lượng. Lưu hoàn toàn bằng Bytes để không bị sai số làm tròn. Backend không cần tính toán gì vì Trigger cân hết."));

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.TotalCapacityBytes)
                .HasDefaultValue(536870912L)
                .HasComment("Tổng dung lượng user có. Khi đăng ký, Trigger số 2 tự động tạo dòng này và nạp sẵn 500MB (536870912 Bytes) làm Free tier.")
                .HasColumnName("total_capacity_bytes");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UsedBytes)
                .HasComment("Dung lượng đã xài. Khi user up file hoặc xóa file (kể cả xóa mềm), Trigger số 4 tự động lấy file_size cộng/trừ vào đây ngay lập tức.")
                .HasColumnName("used_bytes");

            entity.HasOne(d => d.User).WithOne(p => p.UserStorage)
                .HasForeignKey<UserStorage>(d => d.UserId)
                .HasConstraintName("user_storage_user_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
