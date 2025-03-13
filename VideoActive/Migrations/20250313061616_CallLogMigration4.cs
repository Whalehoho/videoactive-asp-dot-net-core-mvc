using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoActive.Migrations
{
    /// <inheritdoc />
    public partial class CallLogMigration4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CallLog",
                table: "CallLog");

            migrationBuilder.RenameTable(
                name: "CallLog",
                newName: "CallLogs");

            migrationBuilder.RenameIndex(
                name: "IX_CallLog_CID",
                table: "CallLogs",
                newName: "IX_CallLogs_CID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CallLogs",
                table: "CallLogs",
                column: "CID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CallLogs",
                table: "CallLogs");

            migrationBuilder.RenameTable(
                name: "CallLogs",
                newName: "CallLog");

            migrationBuilder.RenameIndex(
                name: "IX_CallLogs_CID",
                table: "CallLog",
                newName: "IX_CallLog_CID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CallLog",
                table: "CallLog",
                column: "CID");
        }
    }
}
