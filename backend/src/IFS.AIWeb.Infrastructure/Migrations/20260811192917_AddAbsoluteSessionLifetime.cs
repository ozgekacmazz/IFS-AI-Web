using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IFS.AIWeb.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAbsoluteSessionLifetime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "absolute_expires_at_utc",
                table: "refresh_tokens",
                type: "timestamp with time zone",
                nullable: true);

            // Existing families have retained their original token rows. Derive the
            // session start from the earliest row instead of inventing a sentinel date.
            migrationBuilder.Sql("""
                UPDATE refresh_tokens AS token
                SET absolute_expires_at_utc = family.started_at_utc + INTERVAL '30 days'
                FROM (
                    SELECT family_id, MIN(created_at_utc) AS started_at_utc
                    FROM refresh_tokens
                    GROUP BY family_id
                ) AS family
                WHERE token.family_id = family.family_id;
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "absolute_expires_at_utc",
                table: "refresh_tokens",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "absolute_expires_at_utc",
                table: "refresh_tokens");
        }
    }
}
