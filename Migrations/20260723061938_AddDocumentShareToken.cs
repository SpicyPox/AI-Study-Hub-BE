using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentShareToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "share_token",
                schema: "ai_study_hub",
                table: "documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_documents_share_token",
                schema: "ai_study_hub",
                table: "documents",
                column: "share_token",
                unique: true,
                filter: "share_token IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_documents_share_token",
                schema: "ai_study_hub",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "share_token",
                schema: "ai_study_hub",
                table: "documents");
        }
    }
}
