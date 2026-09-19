using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletConcurrencyVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Wallets",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "Wallets");
        }
    }
}
