using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhenWorksWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantMessageSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventMessages_Participants_ParticipantId",
                table: "EventMessages");

            migrationBuilder.AlterColumn<int>(
                name: "ParticipantId",
                table: "EventMessages",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "SenderColor",
                table: "EventMessages",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SenderDisplayName",
                table: "EventMessages",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_EventMessages_Participants_ParticipantId",
                table: "EventMessages",
                column: "ParticipantId",
                principalTable: "Participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventMessages_Participants_ParticipantId",
                table: "EventMessages");

            migrationBuilder.DropColumn(
                name: "SenderColor",
                table: "EventMessages");

            migrationBuilder.DropColumn(
                name: "SenderDisplayName",
                table: "EventMessages");

            migrationBuilder.AlterColumn<int>(
                name: "ParticipantId",
                table: "EventMessages",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EventMessages_Participants_ParticipantId",
                table: "EventMessages",
                column: "ParticipantId",
                principalTable: "Participants",
                principalColumn: "Id");
        }
    }
}
