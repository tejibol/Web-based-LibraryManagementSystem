using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraryManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class FixRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BorrowingHistories_AspNetUsers_UserId",
                table: "BorrowingHistories");

            migrationBuilder.DropIndex(
                name: "IX_BorrowingHistories_BookID",
                table: "BorrowingHistories");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "BorrowingHistories",
                newName: "UserID");

            migrationBuilder.RenameIndex(
                name: "IX_BorrowingHistories_UserId",
                table: "BorrowingHistories",
                newName: "IX_BorrowingHistories_UserID");

            migrationBuilder.CreateIndex(
                name: "IX_BorrowingHistories_BookID",
                table: "BorrowingHistories",
                column: "BookID");

            migrationBuilder.AddForeignKey(
                name: "FK_BorrowingHistories_AspNetUsers_UserID",
                table: "BorrowingHistories",
                column: "UserID",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BorrowingHistories_AspNetUsers_UserID",
                table: "BorrowingHistories");

            migrationBuilder.DropIndex(
                name: "IX_BorrowingHistories_BookID",
                table: "BorrowingHistories");

            migrationBuilder.RenameColumn(
                name: "UserID",
                table: "BorrowingHistories",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_BorrowingHistories_UserID",
                table: "BorrowingHistories",
                newName: "IX_BorrowingHistories_UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BorrowingHistories_BookID",
                table: "BorrowingHistories",
                column: "BookID",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BorrowingHistories_AspNetUsers_UserId",
                table: "BorrowingHistories",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
