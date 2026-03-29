using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetroTransit.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIsAllDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // KEEP: This removes the foreign key constraint before removing the column
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_AspNetUsers_CreatedByUserId",
                table: "Reservations");

            // KEEP: This drops the IsAllDay column from Reservations
            migrationBuilder.DropColumn(
                name: "IsAllDay",
                table: "Reservations");

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // KEEP: This updates the foreign key column to be non-nullable
            migrationBuilder.AlterColumn<string>(
                name: "CreatedByUserId",
                table: "Reservations",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            // KEEP: This re-adds the foreign key constraint
            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_AspNetUsers_CreatedByUserId",
                table: "Reservations",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The Down method is for rolling back, so it should re-add the column.
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_AspNetUsers_CreatedByUserId",
                table: "Reservations");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByUserId",
                table: "Reservations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "IsAllDay",
                table: "Reservations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "AspNetUsers");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_AspNetUsers_CreatedByUserId",
                table: "Reservations",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
