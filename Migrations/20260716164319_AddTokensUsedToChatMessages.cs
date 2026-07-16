using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTokensUsedToChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "tokens_used",
                schema: "ai_study_hub",
                table: "chat_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tokens_used",
                schema: "ai_study_hub",
                table: "chat_messages");
        }
    }
}
