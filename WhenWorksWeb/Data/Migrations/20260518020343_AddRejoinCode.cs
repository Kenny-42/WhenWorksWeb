using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhenWorksWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRejoinCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Participants_EventId_UserId",
                table: "Participants");

            migrationBuilder.AddColumn<string>(
                name: "RejoinCode",
                table: "Participants",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true,
                collation: "SQL_Latin1_General_CP1_CI_AS");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_EventId_Color",
                table: "Participants",
                columns: new[] { "EventId", "Color" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_EventId_UserId",
                table: "Participants",
                columns: new[] { "EventId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Participants_RejoinCode",
                table: "Participants",
                column: "RejoinCode",
                unique: true,
                filter: "[RejoinCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Participants_EventId_Color",
                table: "Participants");

            migrationBuilder.DropIndex(
                name: "IX_Participants_EventId_UserId",
                table: "Participants");

            migrationBuilder.DropIndex(
                name: "IX_Participants_RejoinCode",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "RejoinCode",
                table: "Participants");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_EventId_UserId",
                table: "Participants",
                columns: new[] { "EventId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");
        }
    }
}
