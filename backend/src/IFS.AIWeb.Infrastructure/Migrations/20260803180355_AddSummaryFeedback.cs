using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IFS.AIWeb.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSummaryFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "feedback",
                table: "summary_records",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "feedback_updated_at_utc",
                table: "summary_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_summary_records_feedback_timestamp",
                table: "summary_records",
                sql: "(feedback IS NULL AND feedback_updated_at_utc IS NULL) OR (feedback IS NOT NULL AND feedback_updated_at_utc IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_summary_records_feedback_value",
                table: "summary_records",
                sql: "feedback IS NULL OR feedback IN ('Useful', 'NotUseful')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_summary_records_feedback_timestamp",
                table: "summary_records");

            migrationBuilder.DropCheckConstraint(
                name: "ck_summary_records_feedback_value",
                table: "summary_records");

            migrationBuilder.DropColumn(
                name: "feedback",
                table: "summary_records");

            migrationBuilder.DropColumn(
                name: "feedback_updated_at_utc",
                table: "summary_records");
        }
    }
}
