using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class TempMigrationToCheckChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "base_storage_bytes",
                schema: "ai_study_hub",
                table: "subscription_packages",
                type: "bigint",
                nullable: false,
                defaultValue: 10485760L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldDefaultValue: 536870912L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "base_storage_bytes",
                schema: "ai_study_hub",
                table: "subscription_packages",
                type: "bigint",
                nullable: false,
                defaultValue: 536870912L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldDefaultValue: 10485760L);
        }
    }
}
