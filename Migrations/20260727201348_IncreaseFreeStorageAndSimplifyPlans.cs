using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class IncreaseFreeStorageAndSimplifyPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "total_capacity_bytes",
                schema: "ai_study_hub",
                table: "user_storage",
                type: "bigint",
                nullable: false,
                defaultValue: 1073741824L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldDefaultValue: 10485760L);

            // Nang free-tier hien huu (chua tung mua gi, van con dung dung luong mac dinh cu) tu 10MB len 1GB.
            migrationBuilder.Sql(@"
                UPDATE ai_study_hub.user_storage
                SET total_capacity_bytes = 1073741824
                WHERE total_capacity_bytes = 10485760;
            ");

            // Gom con 1 goi tra phi duy nhat (Pro, 10GB). An 2 goi cu thay vi xoa de khong vo FK
            // voi UserSubscriptions/Transactions lich su da tham chieu toi chung.
            migrationBuilder.Sql(@"
                UPDATE ai_study_hub.subscription_packages
                SET is_active = false
                WHERE name IN ('Sinh Viên', 'Pro Năm');

                UPDATE ai_study_hub.subscription_packages
                SET base_storage_bytes = 10737418240,
                    description = 'Dành cho học viên chuyên sâu: 10 GB, không giới hạn AI'
                WHERE name = 'Pro';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "total_capacity_bytes",
                schema: "ai_study_hub",
                table: "user_storage",
                type: "bigint",
                nullable: false,
                defaultValue: 10485760L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldDefaultValue: 1073741824L);
        }
    }
}
