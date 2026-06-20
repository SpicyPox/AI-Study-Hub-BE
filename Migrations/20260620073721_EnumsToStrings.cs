using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnumsToStrings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "ai_study_hub",
                table: "transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "ai_study_hub.payment_status");

            migrationBuilder.AlterColumn<string>(
                name: "purchase_kind",
                schema: "ai_study_hub",
                table: "transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "ai_study_hub.purchase_type");

            migrationBuilder.AlterColumn<string>(
                name: "method",
                schema: "ai_study_hub",
                table: "transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "ai_study_hub.payment_method");

            migrationBuilder.AlterColumn<string>(
                name: "visibility",
                schema: "ai_study_hub",
                table: "documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "public",
                oldClrType: typeof(int),
                oldType: "ai_study_hub.doc_visibility",
                oldDefaultValueSql: "'public'");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "ai_study_hub",
                table: "cloud_files",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "ai_study_hub.cloud_status");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                schema: "ai_study_hub",
                table: "chat_messages",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "ai_study_hub.chat_role");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "ai_study_hub",
                table: "transactions",
                type: "ai_study_hub.payment_status",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "purchase_kind",
                schema: "ai_study_hub",
                table: "transactions",
                type: "ai_study_hub.purchase_type",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "method",
                schema: "ai_study_hub",
                table: "transactions",
                type: "ai_study_hub.payment_method",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "visibility",
                schema: "ai_study_hub",
                table: "documents",
                type: "ai_study_hub.doc_visibility",
                nullable: false,
                defaultValueSql: "'public'",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "public");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "ai_study_hub",
                table: "cloud_files",
                type: "ai_study_hub.cloud_status",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "role",
                schema: "ai_study_hub",
                table: "chat_messages",
                type: "ai_study_hub.chat_role",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }
    }
}
