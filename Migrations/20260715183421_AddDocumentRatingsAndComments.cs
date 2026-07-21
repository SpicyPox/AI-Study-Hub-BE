using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentRatingsAndComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_comments",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("document_comments_pkey", x => x.id);
                    table.ForeignKey(
                        name: "document_comments_document_id_fkey",
                        column: x => x.document_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "document_comments_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_ratings",
                schema: "ai_study_hub",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stars = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("document_ratings_pkey", x => new { x.user_id, x.document_id });
                    table.ForeignKey(
                        name: "document_ratings_document_id_fkey",
                        column: x => x.document_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "document_ratings_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_document_comments_document_id",
                schema: "ai_study_hub",
                table: "document_comments",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_comments_user_id",
                schema: "ai_study_hub",
                table: "document_comments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_document_ratings_document_id",
                schema: "ai_study_hub",
                table: "document_ratings",
                column: "document_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_comments",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "document_ratings",
                schema: "ai_study_hub");
        }
    }
}
