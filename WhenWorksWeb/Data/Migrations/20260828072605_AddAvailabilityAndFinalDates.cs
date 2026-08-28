using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhenWorksWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAvailabilityAndFinalDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventFinalDates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventFinalDates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventFinalDates_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParticipantAvailabilities",
                columns: table => new
                {
                    ParticipantId = table.Column<int>(type: "int", nullable: false),
                    EventDateId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantAvailabilities", x => new { x.ParticipantId, x.EventDateId });
                    table.ForeignKey(
                        name: "FK_ParticipantAvailabilities_EventDates_EventDateId",
                        column: x => x.EventDateId,
                        principalTable: "EventDates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParticipantAvailabilities_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventFinalDates_EventId",
                table: "EventFinalDates",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantAvailabilities_EventDateId",
                table: "ParticipantAvailabilities",
                column: "EventDateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventFinalDates");

            migrationBuilder.DropTable(
                name: "ParticipantAvailabilities");
        }
    }
}
