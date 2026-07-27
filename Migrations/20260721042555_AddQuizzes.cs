using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizzes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quizzes",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("quizzes_pkey", x => x.id);
                    table.ForeignKey(
                        name: "quizzes_document_id_fkey",
                        column: x => x.document_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "quizzes_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quiz_attempts",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    quiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    total_questions = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("quiz_attempts_pkey", x => x.id);
                    table.ForeignKey(
                        name: "quiz_attempts_quiz_id_fkey",
                        column: x => x.quiz_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "quizzes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "quiz_attempts_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quiz_questions",
                schema: "ai_study_hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    quiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_text = table.Column<string>(type: "text", nullable: false),
                    option_a = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    option_b = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    option_c = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    option_d = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    correct_index = table.Column<int>(type: "integer", nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: true),
                    order_index = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("quiz_questions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "quiz_questions_quiz_id_fkey",
                        column: x => x.quiz_id,
                        principalSchema: "ai_study_hub",
                        principalTable: "quizzes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_quiz_attempts_quiz_id",
                schema: "ai_study_hub",
                table: "quiz_attempts",
                column: "quiz_id");

            migrationBuilder.CreateIndex(
                name: "idx_quiz_attempts_user_id",
                schema: "ai_study_hub",
                table: "quiz_attempts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_quiz_questions_quiz_id",
                schema: "ai_study_hub",
                table: "quiz_questions",
                column: "quiz_id");

            migrationBuilder.CreateIndex(
                name: "idx_quizzes_document_id",
                schema: "ai_study_hub",
                table: "quizzes",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "idx_quizzes_user_id",
                schema: "ai_study_hub",
                table: "quizzes",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quiz_attempts",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "quiz_questions",
                schema: "ai_study_hub");

            migrationBuilder.DropTable(
                name: "quizzes",
                schema: "ai_study_hub");
        }
    }
}
