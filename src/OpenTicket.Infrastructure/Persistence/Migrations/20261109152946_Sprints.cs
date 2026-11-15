using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTicket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ticket_history_change_type",
                table: "ticket_history");

            migrationBuilder.AddColumn<Guid>(
                name: "sprint_id",
                table: "ticket",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sprint",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    goal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    state = table.Column<short>(type: "smallint", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sprint", x => x.id);
                    table.CheckConstraint("ck_sprint_completed_at", "(state = 2) = (completed_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_sprint_date_order", "end_date IS NULL OR start_date IS NULL OR end_date >= start_date");
                    table.CheckConstraint("ck_sprint_dates", "state = 0 OR (start_date IS NOT NULL AND end_date IS NOT NULL)");
                    table.CheckConstraint("ck_sprint_state", "state BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "fk_sprint_application_user_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "application_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_ticket_history_change_type",
                table: "ticket_history",
                sql: "change_type BETWEEN 0 AND 8");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_sprint_id_status",
                table: "ticket",
                columns: new[] { "sprint_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_sprint_created_by_id",
                table: "sprint",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_sprint_state_created_at_utc",
                table: "sprint",
                columns: new[] { "state", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_sprint_single_active",
                table: "sprint",
                column: "state",
                unique: true,
                filter: "state = 1");

            migrationBuilder.AddForeignKey(
                name: "fk_ticket_sprint_sprint_id",
                table: "ticket",
                column: "sprint_id",
                principalTable: "sprint",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ticket_sprint_sprint_id",
                table: "ticket");

            migrationBuilder.DropTable(
                name: "sprint");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ticket_history_change_type",
                table: "ticket_history");

            migrationBuilder.DropIndex(
                name: "ix_ticket_sprint_id_status",
                table: "ticket");

            migrationBuilder.DropColumn(
                name: "sprint_id",
                table: "ticket");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ticket_history_change_type",
                table: "ticket_history",
                sql: "change_type BETWEEN 0 AND 7");
        }
    }
}
