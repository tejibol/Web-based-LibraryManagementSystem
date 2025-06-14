using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraryManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class RecreatedBookCopyModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookCopy_AspNetUsers_CurrentBorrowerID",
                table: "BookCopy");

            migrationBuilder.DropForeignKey(
                name: "FK_BookCopy_Books_BookID",
                table: "BookCopy");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BookCopy",
                table: "BookCopy");

            migrationBuilder.DropIndex(
                name: "IX_BookCopy_CurrentBorrowerID",
                table: "BookCopy");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "CopyIDNum",
                table: "BookCopy");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "BookCopy");

            migrationBuilder.DropColumn(
                name: "CurrentBorrowerID",
                table: "BookCopy");

            migrationBuilder.DropColumn(
                name: "LastBorrowedDate",
                table: "BookCopy");

            migrationBuilder.DropColumn(
                name: "LastReturnedDate",
                table: "BookCopy");

            migrationBuilder.RenameTable(
                name: "BookCopy",
                newName: "BookCopies");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "BookCopies",
                newName: "CopyNumber");

            migrationBuilder.RenameIndex(
                name: "IX_BookCopy_BookID",
                table: "BookCopies",
                newName: "IX_BookCopies_BookID");

            migrationBuilder.AddColumn<int>(
                name: "BookCopyCopyID",
                table: "BorrowingHistories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CopyID",
                table: "BorrowingHistories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CopyNumber",
                table: "BorrowingHistories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Condition",
                table: "BookCopies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                table: "BookCopies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "BookCopies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_BookCopies",
                table: "BookCopies",
                column: "CopyID");

            migrationBuilder.CreateIndex(
                name: "IX_BorrowingHistories_BookCopyCopyID",
                table: "BorrowingHistories",
                column: "BookCopyCopyID");

            migrationBuilder.AddForeignKey(
                name: "FK_BookCopies_Books_BookID",
                table: "BookCopies",
                column: "BookID",
                principalTable: "Books",
                principalColumn: "BookID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BorrowingHistories_BookCopies_BookCopyCopyID",
                table: "BorrowingHistories",
                column: "BookCopyCopyID",
                principalTable: "BookCopies",
                principalColumn: "CopyID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookCopies_Books_BookID",
                table: "BookCopies");

            migrationBuilder.DropForeignKey(
                name: "FK_BorrowingHistories_BookCopies_BookCopyCopyID",
                table: "BorrowingHistories");

            migrationBuilder.DropIndex(
                name: "IX_BorrowingHistories_BookCopyCopyID",
                table: "BorrowingHistories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BookCopies",
                table: "BookCopies");

            migrationBuilder.DropColumn(
                name: "BookCopyCopyID",
                table: "BorrowingHistories");

            migrationBuilder.DropColumn(
                name: "CopyID",
                table: "BorrowingHistories");

            migrationBuilder.DropColumn(
                name: "CopyNumber",
                table: "BorrowingHistories");

            migrationBuilder.DropColumn(
                name: "Condition",
                table: "BookCopies");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: "BookCopies");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "BookCopies");

            migrationBuilder.RenameTable(
                name: "BookCopies",
                newName: "BookCopy");

            migrationBuilder.RenameColumn(
                name: "CopyNumber",
                table: "BookCopy",
                newName: "Status");

            migrationBuilder.RenameIndex(
                name: "IX_BookCopies_BookID",
                table: "BookCopy",
                newName: "IX_BookCopy_BookID");

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "Books",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CopyIDNum",
                table: "BookCopy",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "BookCopy",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CurrentBorrowerID",
                table: "BookCopy",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastBorrowedDate",
                table: "BookCopy",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReturnedDate",
                table: "BookCopy",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_BookCopy",
                table: "BookCopy",
                column: "CopyID");

            migrationBuilder.CreateIndex(
                name: "IX_BookCopy_CurrentBorrowerID",
                table: "BookCopy",
                column: "CurrentBorrowerID");

            migrationBuilder.AddForeignKey(
                name: "FK_BookCopy_AspNetUsers_CurrentBorrowerID",
                table: "BookCopy",
                column: "CurrentBorrowerID",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BookCopy_Books_BookID",
                table: "BookCopy",
                column: "BookID",
                principalTable: "Books",
                principalColumn: "BookID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
