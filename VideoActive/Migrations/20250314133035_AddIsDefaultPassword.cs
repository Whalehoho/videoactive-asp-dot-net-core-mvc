using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoActive.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDefaultPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultPassword",
                table: "Admins",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDefaultPassword",
                table: "Admins");
        }
    }
}
