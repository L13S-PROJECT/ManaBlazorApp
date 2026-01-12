#nullable disable
using Microsoft.EntityFrameworkCore.Migrations;

namespace ManiApi.Migrations
{
    /// <inheritdoc />
    public partial class Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Baseline: DB shēma jau eksistē. Neveicam izmaiņas.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Baseline: nav ko atgriezt.
        }
    }
}
