using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class ChangeDocVisibilityToVarchar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Doi cot truoc khi xoa enum type: neu xoa type "doc_visibility" truoc, Postgres
            // bao loi 2BP01 vi cot "visibility" van con tham chieu toi type do.
            migrationBuilder.AlterColumn<string>(
                name: "visibility",
                schema: "ai_study_hub",
                table: "documents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "public",
                oldClrType: typeof(int),
                oldType: "ai_study_hub.doc_visibility",
                oldDefaultValueSql: "'public'");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:ai_study_hub.chat_role", "user,assistant")
                .Annotation("Npgsql:Enum:ai_study_hub.cloud_status", "pending,uploaded,failed")
                .Annotation("Npgsql:Enum:ai_study_hub.payment_method", "vnpay,momo,stripe,bank_transfer")
                .Annotation("Npgsql:Enum:ai_study_hub.payment_status", "pending,completed,failed,refunded")
                .Annotation("Npgsql:Enum:ai_study_hub.user_role", "user,admin")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.chat_role", "user,assistant")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.cloud_status", "pending,uploaded,failed")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.doc_visibility", "public,private")
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
                .Annotation("Npgsql:Enum:ai_study_hub.doc_visibility", "public,private")
                .Annotation("Npgsql:Enum:ai_study_hub.payment_method", "vnpay,momo,stripe,bank_transfer")
                .Annotation("Npgsql:Enum:ai_study_hub.payment_status", "pending,completed,failed,refunded")
                .Annotation("Npgsql:Enum:ai_study_hub.user_role", "user,admin")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.chat_role", "user,assistant")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.cloud_status", "pending,uploaded,failed")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.payment_method", "vnpay,momo,stripe,bank_transfer")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.payment_status", "pending,completed,failed,refunded")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.user_role", "user,admin");

            migrationBuilder.AlterColumn<int>(
                name: "visibility",
                schema: "ai_study_hub",
                table: "documents",
                type: "ai_study_hub.doc_visibility",
                nullable: false,
                defaultValueSql: "'public'",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "public");
        }
    }
}
