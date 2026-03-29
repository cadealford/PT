using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetroTransit.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDepartmentFromPersonnel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Department",
                table: "Personnel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Personnel",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
