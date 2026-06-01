using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhenWorksWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateParticipantMessageSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventMessages_Participants_ParticipantId",
                table: "EventMessages");

            migrationBuilder.AddForeignKey(
                name: "FK_EventMessages_Participants_ParticipantId",
                table: "EventMessages",
                column: "ParticipantId",
                principalTable: "Participants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventMessages_Participants_ParticipantId",
                table: "EventMessages");

            migrationBuilder.AddForeignKey(
                name: "FK_EventMessages_Participants_ParticipantId",
                table: "EventMessages",
                column: "ParticipantId",
                principalTable: "Participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
