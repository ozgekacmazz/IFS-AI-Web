using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IFS.AIWeb.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSummarizationRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "summary_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    input_text = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    summary_text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    requested_language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    prompt_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    input_character_count = table.Column<int>(type: "integer", nullable: false),
                    output_character_count = table.Column<int>(type: "integer", nullable: false),
                    duration_milliseconds = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    failure_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_summary_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_summary_records_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_summary_records_created_at",
                table: "summary_records",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_summary_records_expires_at",
                table: "summary_records",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_summary_records_user_status_created",
                table: "summary_records",
                columns: new[] { "user_id", "status", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "summary_records");
        }
    }
}
