using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class ChangeCloudStatusAndChatRoleToVarchar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Doi cot truoc khi xoa enum type (xem ghi chu cung loi trong migration truoc do).
            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "ai_study_hub",
                table: "cloud_files",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "pending",
                oldClrType: typeof(int),
                oldType: "ai_study_hub.cloud_status");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                schema: "ai_study_hub",
                table: "chat_messages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "ai_study_hub.chat_role");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:ai_study_hub.payment_method", "vnpay,momo,stripe,bank_transfer")
                .Annotation("Npgsql:Enum:ai_study_hub.payment_status", "pending,completed,failed,refunded")
                .Annotation("Npgsql:Enum:ai_study_hub.user_role", "user,admin")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.chat_role", "user,assistant")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.cloud_status", "pending,uploaded,failed")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.payment_method", "vnpay,momo,stripe,bank_transfer")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.payment_status", "pending,completed,failed,refunded")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.user_role", "user,admin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:ai_study_hub.chat_role", "user,assistant")
                .Annotation("Npgsql:Enum:ai_study_hub.cloud_status", "pending,uploaded,failed")
                .Annotation("Npgsql:Enum:ai_study_hub.payment_method", "vnpay,momo,stripe,bank_transfer")
                .Annotation("Npgsql:Enum:ai_study_hub.payment_status", "pending,completed,failed,refunded")
                .Annotation("Npgsql:Enum:ai_study_hub.user_role", "user,admin")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.payment_method", "vnpay,momo,stripe,bank_transfer")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.payment_status", "pending,completed,failed,refunded")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.user_role", "user,admin");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "ai_study_hub",
                table: "cloud_files",
                type: "ai_study_hub.cloud_status",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "pending");

            migrationBuilder.AlterColumn<int>(
                name: "role",
                schema: "ai_study_hub",
                table: "chat_messages",
                type: "ai_study_hub.chat_role",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);
        }
    }
}
