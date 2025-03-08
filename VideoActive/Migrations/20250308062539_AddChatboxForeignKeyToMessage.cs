using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoActive.Migrations
{
    /// <inheritdoc />
    public partial class AddChatboxForeignKeyToMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CID",
                table: "Messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_CID",
                table: "Messages",
                column: "CID");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Chatboxes_CID",
                table: "Messages",
                column: "CID",
                principalTable: "Chatboxes",
                principalColumn: "CID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Chatboxes_CID",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_CID",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "CID",
                table: "Messages");
        }
    }
}
