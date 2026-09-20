using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    [DbContext(typeof(PostgresServerDbContext))]
    [Migration("20260920000000_SerenityPlayerResources")]
    public partial class SerenityPlayerResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "serenity_player_resource",
                columns: table => new
                {
                    serenity_player_resource_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<double>(type: "double precision", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_serenity_player_resource", x => x.serenity_player_resource_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_serenity_player_resource_player_id_resource",
                table: "serenity_player_resource",
                columns: new[] { "player_id", "resource" },
                unique: true);

            migrationBuilder.CreateTable(
                name: "serenity_resource_transaction",
                columns: table => new
                {
                    serenity_resource_transaction_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource = table.Column<string>(type: "text", nullable: false),
                    delta = table.Column<double>(type: "double precision", nullable: false),
                    balance_after = table.Column<double>(type: "double precision", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_serenity_resource_transaction", x => x.serenity_resource_transaction_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_serenity_resource_transaction_player_id",
                table: "serenity_resource_transaction",
                column: "player_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "serenity_resource_transaction");
            migrationBuilder.DropTable(name: "serenity_player_resource");
        }
    }
}
