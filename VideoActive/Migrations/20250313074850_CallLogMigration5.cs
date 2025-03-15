using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoActive.Migrations
{
    /// <inheritdoc />
    public partial class CallLogMigration5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CallerID",
                table: "CallLogs",
                newName: "CallerId");

            migrationBuilder.RenameColumn(
                name: "CalleeID",
                table: "CallLogs",
                newName: "CalleeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CallerId",
                table: "CallLogs",
                newName: "CallerID");

            migrationBuilder.RenameColumn(
                name: "CalleeId",
                table: "CallLogs",
                newName: "CalleeID");
        }
    }
}
