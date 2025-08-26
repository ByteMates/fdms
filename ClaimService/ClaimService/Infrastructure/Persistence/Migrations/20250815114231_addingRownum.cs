using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class addingRownum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Claims",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ClaimEvents",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ClaimEvents");
        }
    }
}
