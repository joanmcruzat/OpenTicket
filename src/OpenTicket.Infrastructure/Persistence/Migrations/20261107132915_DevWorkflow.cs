using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTicket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DevWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ticket_history_change_type",
                table: "ticket_history");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ticket_cancel_note",
                table: "ticket");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ticket_closure_reason",
                table: "ticket");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ticket_status",
                table: "ticket");

            // Existing tickets become Tasks (1). New rows always get an explicit type from the domain.
            migrationBuilder.AddColumn<short>(
                name: "type",
                table: "ticket",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            // Old → new status: Open(0)→To Do(1), In Progress(1)→2, Resolved(2)→In Review(3),
            // Closed(3)+Completed→Done(4), Closed(3)+Cancelled→Won't Do(5). Closed tickets already have closed_at_utc.
            migrationBuilder.Sql("""
                UPDATE ticket SET status = CASE
                    WHEN status = 3 AND closure_reason = 1 THEN 5
                    WHEN status = 3 THEN 4
                    ELSE status + 1
                END;
                UPDATE ticket SET closure_note = NULL WHERE status = 4;
                """);

            // Keep the audit trail readable with the new status names.
            migrationBuilder.Sql("""
                UPDATE ticket_history SET old_value = CASE old_value
                    WHEN 'Open' THEN 'ToDo' WHEN 'Resolved' THEN 'InReview' WHEN 'Closed' THEN 'Done' ELSE old_value END
                WHERE change_type IN (0, 1);
                UPDATE ticket_history SET new_value = CASE
                    WHEN new_value = 'Open' THEN 'ToDo'
                    WHEN new_value = 'Resolved' THEN 'InReview'
                    WHEN new_value = 'Closed' THEN 'Done'
                    WHEN new_value LIKE 'Closed (Cancelled: %)' THEN 'WontDo: ' || substr(new_value, 20, length(new_value) - 20)
                    ELSE new_value END
                WHERE change_type IN (0, 1);
                """);

            migrationBuilder.DropColumn(
                name: "closure_reason",
                table: "ticket");

            migrationBuilder.DropColumn(
                name: "resolved_at_utc",
                table: "ticket");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ticket_history_change_type",
                table: "ticket_history",
                sql: "change_type BETWEEN 0 AND 7");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ticket_closed_at",
                table: "ticket",
                sql: "(status >= 4) = (closed_at_utc IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ticket_status",
                table: "ticket",
                sql: "status BETWEEN 0 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ticket_type",
                table: "ticket",
                sql: "type BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ticket_wont_do_note",
                table: "ticket",
                sql: "status <> 5 OR (closure_note IS NOT NULL AND length(trim(closure_note)) > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ticket_history_change_type",
                table: "ticket_history");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ticket_closed_at",
                table: "ticket");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ticket_status",
                table: "ticket");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ticket_type",
                table: "ticket");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ticket_wont_do_note",
                table: "ticket");

            migrationBuilder.DropColumn(
                name: "type",
                table: "ticket");

            migrationBuilder.AddColumn<short>(
                name: "closure_reason",
                table: "ticket",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "resolved_at_utc",
                table: "ticket",
                type: "timestamp with time zone",
                nullable: true);

            // Best-effort reverse mapping: Backlog and To Do → Open, In Review → Resolved, Done/Won't Do → Closed.
            // Type changes in the history cannot be represented and are removed.
            migrationBuilder.Sql("""
                UPDATE ticket SET
                    closure_reason = CASE status WHEN 4 THEN 0 WHEN 5 THEN 1 END,
                    status = CASE status WHEN 0 THEN 0 WHEN 1 THEN 0 WHEN 2 THEN 1 WHEN 3 THEN 2 ELSE 3 END;
                DELETE FROM ticket_history WHERE change_type = 7;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_ticket_history_change_type",
                table: "ticket_history",
                sql: "change_type BETWEEN 0 AND 6");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ticket_cancel_note",
                table: "ticket",
                sql: "closure_reason IS DISTINCT FROM 1 OR (closure_note IS NOT NULL AND length(trim(closure_note)) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ticket_closure_reason",
                table: "ticket",
                sql: "(status = 3) = (closure_reason IS NOT NULL) AND (closure_reason IS NULL OR closure_reason BETWEEN 0 AND 1)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ticket_status",
                table: "ticket",
                sql: "status BETWEEN 0 AND 3");
        }
    }
}
