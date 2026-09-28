using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class SerenityDiscordLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "serenity_discord_link",
                columns: table => new
                {
                    player_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    discord_id = table.Column<long>(type: "INTEGER", nullable: false),
                    discord_username = table.Column<string>(type: "TEXT", nullable: true),
                    linked_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_serenity_discord_link", x => x.player_user_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_serenity_discord_link_discord_id",
                table: "serenity_discord_link",
                column: "discord_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "serenity_discord_link");
        }
    }
}
