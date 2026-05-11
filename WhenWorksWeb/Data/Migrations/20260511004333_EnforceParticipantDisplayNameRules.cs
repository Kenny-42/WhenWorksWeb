using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhenWorksWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceParticipantDisplayNameRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Participants",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                collation: "SQL_Latin1_General_CP1_CS_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Participants_DisplayName_Trimmed",
                table: "Participants",
                sql: "[DisplayName] = LTRIM(RTRIM([DisplayName]))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Participants_DisplayName_Trimmed",
                table: "Participants");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Participants",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldCollation: "SQL_Latin1_General_CP1_CS_AS");
        }
    }
}
