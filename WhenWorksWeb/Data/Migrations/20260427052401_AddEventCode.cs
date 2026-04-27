using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhenWorksWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Events",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "",
                collation: "SQL_Latin1_General_CP1_CI_AS");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Code",
                table: "Events",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_Code",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Events");
        }
    }
}
