using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class DropSubjectNameUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ten mon hoc KHONG duoc phep la khoa duy nhat toan he thong: cung 1 ten "Toan cao
            // cap" co the la 2 mon hop le khac nhau o 2 nganh khac nhau (vd TOAN-CNTT vs
            // TOAN-KT), chi khac o Ma mon. Ma mon (subjects_code_key, da co san) moi la khoa
            // xac dinh 1 mon hoc duy nhat - bo rang buoc unique tren ten da them nham o
            // migration truoc.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ai_study_hub.subjects_name_lower_key;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS subjects_name_lower_key
                ON ai_study_hub.subjects (lower(trim(name)));
            ");
        }
    }
}
