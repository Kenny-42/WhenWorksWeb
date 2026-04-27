using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhenWorksWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantAndEventModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Events");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LastActiveAt",
                table: "Events",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Events",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "Events",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EventDates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventDates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventDates_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventSettings",
                columns: table => new
                {
                    EventId = table.Column<int>(type: "int", nullable: false),
                    Emoji = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSettings", x => x.EventId);
                    table.ForeignKey(
                        name: "FK_EventSettings_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Participants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Participants_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Participants_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserEventBookmarks",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EventId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEventBookmarks", x => new { x.UserId, x.EventId });
                    table.ForeignKey(
                        name: "FK_UserEventBookmarks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserEventBookmarks_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    ParticipantId = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventMessages_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventMessages_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EventRoles",
                columns: table => new
                {
                    ParticipantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventRoles", x => x.ParticipantId);
                    table.ForeignKey(
                        name: "FK_EventRoles_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_CreatedByUserId",
                table: "Events",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_LastActiveAt",
                table: "Events",
                column: "LastActiveAt");

            migrationBuilder.CreateIndex(
                name: "IX_EventDates_EventId_Date",
                table: "EventDates",
                columns: new[] { "EventId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventMessages_EventId",
                table: "EventMessages",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventMessages_EventId_SentAt",
                table: "EventMessages",
                columns: new[] { "EventId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EventMessages_ParticipantId",
                table: "EventMessages",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_EventMessages_SentAt",
                table: "EventMessages",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_EventId_DisplayName",
                table: "Participants",
                columns: new[] { "EventId", "DisplayName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_EventId_UserId",
                table: "Participants",
                columns: new[] { "EventId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_UserId",
                table: "Participants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserEventBookmarks_EventId",
                table: "UserEventBookmarks",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_AspNetUsers_CreatedByUserId",
                table: "Events",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_AspNetUsers_CreatedByUserId",
                table: "Events");

            migrationBuilder.DropTable(
                name: "EventDates");

            migrationBuilder.DropTable(
                name: "EventMessages");

            migrationBuilder.DropTable(
                name: "EventRoles");

            migrationBuilder.DropTable(
                name: "EventSettings");

            migrationBuilder.DropTable(
                name: "UserEventBookmarks");

            migrationBuilder.DropTable(
                name: "Participants");

            migrationBuilder.DropIndex(
                name: "IX_Events_CreatedByUserId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_LastActiveAt",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Events");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastActiveAt",
                table: "Events",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Events",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Events",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }
    }
}
