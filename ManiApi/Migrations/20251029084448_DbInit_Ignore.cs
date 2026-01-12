using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManiApi.Migrations
{
    /// <inheritdoc />
    public partial class DbInit_Ignore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tukša — ignorējam, jo tabulas jau eksistē datubāzē
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Tukša — nav ko dzēst
        }
    }
}
