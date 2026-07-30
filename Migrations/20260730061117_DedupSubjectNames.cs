using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class DedupSubjectNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Gop cac subject trung ten (khong phan biet hoa/thuong, da trim): giu ban ghi tao
            // som nhat, chuyen tai lieu dang gan cac ban ghi trung sang ban ghi duoc giu lai,
            // roi xoa cac ban ghi trung - de co the them unique index ben duoi ma khong loi.
            migrationBuilder.Sql(@"
                WITH ranked AS (
                    SELECT id,
                           row_number() OVER (
                               PARTITION BY lower(trim(name))
                               ORDER BY created_at ASC, id ASC
                           ) AS rn,
                           first_value(id) OVER (
                               PARTITION BY lower(trim(name))
                               ORDER BY created_at ASC, id ASC
                           ) AS survivor_id
                    FROM ai_study_hub.subjects
                )
                UPDATE ai_study_hub.documents d
                SET subject_id = r.survivor_id
                FROM ranked r
                WHERE d.subject_id = r.id AND r.rn > 1;
            ");

            migrationBuilder.Sql(@"
                WITH ranked AS (
                    SELECT id,
                           row_number() OVER (
                               PARTITION BY lower(trim(name))
                               ORDER BY created_at ASC, id ASC
                           ) AS rn
                    FROM ai_study_hub.subjects
                )
                DELETE FROM ai_study_hub.subjects s
                USING ranked r
                WHERE s.id = r.id AND r.rn > 1;
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS subjects_name_lower_key
                ON ai_study_hub.subjects (lower(trim(name)));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ai_study_hub.subjects_name_lower_key;");
        }
    }
}
