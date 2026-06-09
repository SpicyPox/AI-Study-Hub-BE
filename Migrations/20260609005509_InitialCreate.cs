using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ai_study_hub");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:ai_study_hub.chat_role", "user,assistant")
                .Annotation("Npgsql:Enum:ai_study_hub.cloud_status", "pending,uploaded,failed")
                .Annotation("Npgsql:Enum:ai_study_hub.doc_visibility", "public,private")
                .Annotation("Npgsql:Enum:ai_study_hub.payment_method", "vnpay,momo,stripe,bank_transfer")
                .Annotation("Npgsql:Enum:ai_study_hub.payment_status", "pending,completed,failed,refunded")
                .Annotation("Npgsql:Enum:ai_study_hub.user_role", "user,admin");

            migrationBuilder.CreateTable(
                name: "storage_packages",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    capacity_bytes = table.Column<long>(type: "bigint", nullable: false),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("storage_packages_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subjects",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("subjects_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("tags_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "Dùng VARCHAR(255) kết hợp UNIQUE INDEX LOWER() để ép hệ thống hiểu \"Email@gmail\" và \"email@gmail\" là một, chống tạo 2 tài khoản trùng lặp. Đã bỏ CITEXT để tránh lỗi phân quyền trên Cloud."),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "Nguyên tắc tử huyệt: Không bao giờ lưu mật khẩu gốc (plaintext). Cột này lưu chuỗi đã mã hóa một chiều (Bcrypt/Argon2)."),
                    role = table.Column<string>(type: "ai_study_hub.user_role", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("users_pkey", x => x.id);
                },
                comment: "Lưu thông tin cốt lõi. Dùng UUID thay vì ID (1,2,3) để bảo mật, chống đối thủ đoán số lượng user và dễ scale server.");

            migrationBuilder.CreateTable(
                name: "chat_sessions",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("chat_sessions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "chat_sessions_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    file_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    file_size = table.Column<long>(type: "bigint", nullable: true, comment: "Bắt buộc dùng BIGINT để lưu số Bytes. Nếu dùng INT bình thường, file >2GB sẽ bị tràn bộ nhớ (overflow) gây sập hệ thống."),
                    visibility = table.Column<string>(type: "ai_study_hub.doc_visibility", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Cờ Xóa mềm (Soft Delete). Đổi thành TRUE thì file chui vào thùng rác, giữ lại được 30 ngày để khôi phục thay vì bốc hơi vĩnh viễn."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("documents_pkey", x => x.id);
                    table.ForeignKey(
                        name: "documents_subject_id_fkey",
                        column: x => x.subject_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "documents_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Trái tim của hệ thống. Áp dụng cơ chế ON DELETE CASCADE từ bảng Users: Xóa user là tự động quét sạch tài liệu, không lo rác DB.");

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("password_reset_tokens_pkey", x => x.id);
                    table.ForeignKey(
                        name: "password_reset_tokens_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Quản lý token quên mật khẩu. Có cột expires_at (hạn dùng) và is_used (đã dùng) để vô hiệu hóa link cũ, chống hack.");

            migrationBuilder.CreateTable(
                name: "transactions",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    storage_added_bytes = table.Column<long>(type: "bigint", nullable: false, comment: "Bí quyết linh hoạt: Khách mua gói 10GB hay nhập tay 3.5GB thì Backend chỉ việc quy ra Bytes ném vào đây. Hóa đơn completed là Trigger số 3 tự bốc số này cộng thẳng vào ví storage."),
                    status = table.Column<string>(type: "ai_study_hub.payment_status", nullable: false),
                    method = table.Column<string>(type: "ai_study_hub.payment_method", nullable: false),
                    transaction_ref = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("transactions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "transactions_package_id_fkey",
                        column: x => x.package_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "storage_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "transactions_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Lưu lịch sử nạp tiền. Chìa khóa của mô hình Mua bao nhiêu dùng bấy nhiêu.");

            migrationBuilder.CreateTable(
                name: "user_storage",
                schema: "ai_study_hub",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_capacity_bytes = table.Column<long>(type: "bigint", nullable: false, defaultValue: 536870912L, comment: "Tổng dung lượng user có. Khi đăng ký, Trigger số 2 tự động tạo dòng này và nạp sẵn 500MB (536870912 Bytes) làm Free tier."),
                    used_bytes = table.Column<long>(type: "bigint", nullable: false, comment: "Dung lượng đã xài. Khi user up file hoặc xóa file (kể cả xóa mềm), Trigger số 4 tự động lấy file_size cộng/trừ vào đây ngay lập tức."),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_storage_pkey", x => x.user_id);
                    table.ForeignKey(
                        name: "user_storage_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Ví dung lượng. Lưu hoàn toàn bằng Bytes để không bị sai số làm tròn. Backend không cần tính toán gì vì Trigger cân hết.");

            migrationBuilder.CreateTable(
                name: "chat_messages",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "ai_study_hub.chat_role", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("chat_messages_pkey", x => x.id);
                    table.ForeignKey(
                        name: "chat_messages_session_id_fkey",
                        column: x => x.session_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "chat_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chat_document_context",
                schema: "ai_study_hub",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("chat_document_context_pkey", x => new { x.session_id, x.document_id });
                    table.ForeignKey(
                        name: "chat_document_context_document_id_fkey",
                        column: x => x.document_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "chat_document_context_session_id_fkey",
                        column: x => x.session_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "chat_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Bảng mấu chốt của AI RAG (Chat với tài liệu). Lưu liên kết giữa phiên chat và file PDF. Backend nhìn vào đây để lấy nội dung file kẹp vào Prompt gửi cho AI, giúp AI có \"phao cứu sinh\" trả lời chuẩn xác, không bịa đặt thông tin.");

            migrationBuilder.CreateTable(
                name: "cloud_files",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cloud_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    cloud_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    status = table.Column<string>(type: "ai_study_hub.cloud_status", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("cloud_files_pkey", x => x.id);
                    table.ForeignKey(
                        name: "cloud_files_document_id_fkey",
                        column: x => x.document_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Lưu link S3/MinIO thực tế. Tách riêng bảng này để lỡ mai mốt chê AWS đắt, đổi sang Google Cloud thì chỉ sửa ở đây, không ảnh hưởng logic Documents.");

            migrationBuilder.CreateTable(
                name: "document_tags",
                schema: "ai_study_hub",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("document_tags_pkey", x => new { x.document_id, x.tag_id });
                    table.ForeignKey(
                        name: "document_tags_document_id_fkey",
                        column: x => x.document_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "document_tags_tag_id_fkey",
                        column: x => x.tag_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chat_document_context_document_id",
                schema: "ai_study_hub",
                table: "chat_document_context",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_session_id",
                schema: "ai_study_hub",
                table: "chat_messages",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_sessions_user_id",
                schema: "ai_study_hub",
                table: "chat_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "cloud_files_document_id_key",
                schema: "ai_study_hub",
                table: "cloud_files",
                column: "document_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_tags_tag_id",
                schema: "ai_study_hub",
                table: "document_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "idx_documents_subject_id",
                schema: "ai_study_hub",
                table: "documents",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "idx_documents_user_id",
                schema: "ai_study_hub",
                table: "documents",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_password_tokens_user_id",
                schema: "ai_study_hub",
                table: "password_reset_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "password_reset_tokens_token_key",
                schema: "ai_study_hub",
                table: "password_reset_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "subjects_code_key",
                schema: "ai_study_hub",
                table: "subjects",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "tags_name_key",
                schema: "ai_study_hub",
                table: "tags",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_transactions_user_id",
                schema: "ai_study_hub",
                table: "transactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_package_id",
                schema: "ai_study_hub",
                table: "transactions",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "users_username_key",
                schema: "ai_study_hub",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_document_context",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "chat_messages",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "cloud_files",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "document_tags",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "password_reset_tokens",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "transactions",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "user_storage",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "chat_sessions",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "tags",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "storage_packages",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "subjects",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "users",
                schema: "ai_study_hub");
        }
    }
}
