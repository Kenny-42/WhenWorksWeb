using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhenWorksWeb.Data.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Data-only migration: no model change for the EF CLI to detect (AspNetUsers.PhoneNumber is
    /// part of Identity's base schema and stays mapped -- see
    /// Spec/Features/FEATURES-remove-phone-number-collection.ospec), so the Up()/Down() bodies
    /// below are hand-written SQL rather than CLI-generated, as a one-off exception to
    /// CODING_CONVENTIONS.md's "never hand-edit migrations" rule for this data-clearing case.
    /// </remarks>
    public partial class ClearStoredPhoneNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The app no longer collects or displays phone numbers (see
            // Spec/Features/FEATURES-remove-phone-number-collection.ospec) -- clear out any
            // values already stored so we're not retaining data we have no use for.
            migrationBuilder.Sql(
                "UPDATE [AspNetUsers] SET [PhoneNumber] = NULL, [PhoneNumberConfirmed] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data cleared by Up() can't be recovered -- nothing to reverse.
        }
    }
}
