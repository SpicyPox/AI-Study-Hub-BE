using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRefreshToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "refresh_token",
                schema: "ai_study_hub",
                table: "users");

            migrationBuilder.DropColumn(
                name: "refresh_token_expiry",
                schema: "ai_study_hub",
                table: "users");
        }
    }
}
