using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoFactorAndUserSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "refresh_token_expiry",
                schema: "ai_study_hub",
                table: "users");

            // Không dùng RenameColumn ở đây: refresh_token cũ chứa token phiên đăng nhập hiện có
            // (plaintext), không phải secret TOTP mã hoá AES-GCM. Rename sẽ vô tình mang dữ liệu
            // cũ không tương thích sang cột mới, khiến TotpService.Decrypt ném lỗi. Drop + add mới.
            migrationBuilder.DropColumn(
                name: "refresh_token",
                schema: "ai_study_hub",
                table: "users");

            migrationBuilder.AddColumn<string>(
                name: "two_factor_secret",
                schema: "ai_study_hub",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "two_factor_enabled",
                schema: "ai_study_hub",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "two_factor_pending_secret",
                schema: "ai_study_hub",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_sessions",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refresh_token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    device_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_active_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_sessions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "user_sessions_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_user_sessions_user_id",
                schema: "ai_study_hub",
                table: "user_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "user_sessions_refresh_token_hash_key",
                schema: "ai_study_hub",
                table: "user_sessions",
                column: "refresh_token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_sessions",
                schema: "ai_study_hub");

            migrationBuilder.DropColumn(
                name: "two_factor_enabled",
                schema: "ai_study_hub",
                table: "users");

            migrationBuilder.DropColumn(
                name: "two_factor_pending_secret",
                schema: "ai_study_hub",
                table: "users");

            migrationBuilder.DropColumn(
                name: "two_factor_secret",
                schema: "ai_study_hub",
                table: "users");

            migrationBuilder.AddColumn<string>(
                name: "refresh_token",
                schema: "ai_study_hub",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "refresh_token_expiry",
                schema: "ai_study_hub",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
