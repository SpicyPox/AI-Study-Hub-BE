using System;
using AIStudyHub.Api.Models;
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
                .Annotation("Npgsql:Enum:ai_study_hub.chat_role", "assistant,user")
                .Annotation("Npgsql:Enum:ai_study_hub.cloud_status", "failed,pending,uploaded")
                .Annotation("Npgsql:Enum:ai_study_hub.doc_visibility", "private,public")
                .Annotation("Npgsql:Enum:ai_study_hub.payment_method", "bank_transfer,momo,stripe,vnpay")
                .Annotation("Npgsql:Enum:ai_study_hub.payment_status", "completed,failed,pending,refunded")
                .Annotation("Npgsql:Enum:ai_study_hub.purchase_type", "storage_package,subscription_package");

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("roles_pkey", x => x.id);
                });

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
                name: "subscription_packages",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    ai_chat_limit = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    base_storage_bytes = table.Column<long>(type: "bigint", nullable: false, defaultValue: 536870912L),
                    is_active = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("subscription_packages_pkey", x => x.id);
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
                    role_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    refresh_token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    refresh_token_expiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("users_pkey", x => x.id);
                    table.ForeignKey(
                        name: "users_role_id_fkey",
                        column: x => x.role_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
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
                    visibility = table.Column<DocVisibility>(type: "\"ai_study_hub.doc_visibility\"", nullable: false),
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
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscription_package_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purchase_kind = table.Column<PurchaseType>(type: "\"ai_study_hub.purchase_type\"", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    storage_added_bytes = table.Column<long>(type: "bigint", nullable: false, comment: "Bí quyết linh hoạt: Khách mua gói 10GB hay nhập tay 3.5GB thì Backend chỉ việc quy ra Bytes ném vào đây. Hóa đơn completed là Trigger số 3 tự bốc số này cộng thẳng vào ví storage."),
                    status = table.Column<PaymentStatus>(type: "\"ai_study_hub.payment_status\"", nullable: false),
                    method = table.Column<PaymentMethod>(type: "\"ai_study_hub.payment_method\"", nullable: false),
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
                        name: "transactions_subscription_package_id_fkey",
                        column: x => x.subscription_package_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "subscription_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "transactions_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_storage",
                schema: "ai_study_hub",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_capacity_bytes = table.Column<long>(type: "bigint", nullable: false, defaultValue: 536870912L),
                    used_bytes = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
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
                });

            migrationBuilder.CreateTable(
                name: "user_subscriptions",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_subscriptions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "user_subscriptions_package_id_fkey",
                        column: x => x.package_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "subscription_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "user_subscriptions_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chat_messages",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<ChatRole>(type: "\"ai_study_hub.chat_role\"", nullable: false),
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
                });

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
                    status = table.Column<CloudStatus>(type: "\"ai_study_hub.cloud_status\"", nullable: false),
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
                });

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

            migrationBuilder.CreateTable(
                name: "favorites",
                schema: "ai_study_hub",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("favorites_pkey", x => new { x.user_id, x.document_id });
                    table.ForeignKey(
                        name: "favorites_document_id_fkey",
                        column: x => x.document_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "favorites_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "users",
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
                name: "IX_favorites_document_id",
                schema: "ai_study_hub",
                table: "favorites",
                column: "document_id");

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
                name: "roles_name_key",
                schema: "ai_study_hub",
                table: "roles",
                column: "name",
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
                name: "IX_transactions_subscription_package_id",
                schema: "ai_study_hub",
                table: "transactions",
                column: "subscription_package_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_subscriptions_user",
                schema: "ai_study_hub",
                table: "user_subscriptions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_package_id",
                schema: "ai_study_hub",
                table: "user_subscriptions",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_role_id",
                schema: "ai_study_hub",
                table: "users",
                column: "role_id");

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
                name: "favorites",
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
                name: "user_subscriptions",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "chat_sessions",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "tags",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "storage_packages",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "subscription_packages",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "subjects",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "users",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "ai_study_hub");
        }
    }
}
