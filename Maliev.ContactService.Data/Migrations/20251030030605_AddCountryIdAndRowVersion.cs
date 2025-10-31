using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.ContactService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCountryIdAndRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CountryId",
                table: "ContactMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ContactMessages",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            // Add composite index for duplicate detection (Email + CreatedAt)
            migrationBuilder.CreateIndex(
                name: "IX_ContactMessages_Email_CreatedAt",
                table: "ContactMessages",
                columns: new[] { "Email", "CreatedAt" });

            // Add index for admin filtering (Status + ContactType)
            migrationBuilder.CreateIndex(
                name: "IX_ContactMessages_Status_ContactType",
                table: "ContactMessages",
                columns: new[] { "Status", "ContactType" });

            // Add filtered index for priority triage (Priority + Status where Status IN (0,1))
            migrationBuilder.Sql(@"
                CREATE INDEX ""IX_ContactMessages_Priority_Status_Filtered""
                ON ""ContactMessages"" (""Priority"", ""Status"")
                WHERE ""Status"" IN (0, 1);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes first
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ContactMessages_Priority_Status_Filtered"";");

            migrationBuilder.DropIndex(
                name: "IX_ContactMessages_Status_ContactType",
                table: "ContactMessages");

            migrationBuilder.DropIndex(
                name: "IX_ContactMessages_Email_CreatedAt",
                table: "ContactMessages");

            migrationBuilder.DropColumn(
                name: "CountryId",
                table: "ContactMessages");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ContactMessages");
        }
    }
}
