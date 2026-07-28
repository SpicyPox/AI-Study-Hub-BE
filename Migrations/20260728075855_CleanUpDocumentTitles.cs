using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class CleanUpDocumentTitles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dong bo title cua cac document da co san voi format moi ap dung cho upload
            // (xem FormatTitleFromFileName trong DocumentsController): bo phan mo rong file
            // (.pdf, .docx...) va doi '_' thanh khoang trang. Chi dong vao nhung row title
            // van con dang ket thuc bang dung phan mo rong cua chinh file_type do (tranh dung
            // vao title da duoc user tu doi ten khong con giong ten file goc).
            migrationBuilder.Sql(@"
                UPDATE ai_study_hub.documents
                SET title = trim(replace(regexp_replace(title, '\.' || file_type || '$', '', 'i'), '_', ' '))
                WHERE file_type IS NOT NULL AND title ILIKE '%.' || file_type;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Khong the dao nguoc chinh xac: khong biet khoang trang nao trong title von la
            // dau '_' de doi lai, va viec noi lai duoi file co the trung voi title da bi user
            // sua sau do. Bo qua rollback du lieu cho migration lam sach mot chieu nay.
        }
    }
}
