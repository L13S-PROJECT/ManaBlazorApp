using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManiApi.Migrations
{
    public partial class SyncCurrentDatabaseModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Apzināti tukša: MariaDB struktūra jau tika izveidota manuāli.
            // Šī migrācija tikai sinhronizē EF Core modeļa snapshot.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Apzināti tukša: nedrīkst dzēst esošās MariaDB tabulas.
        }
    }
}