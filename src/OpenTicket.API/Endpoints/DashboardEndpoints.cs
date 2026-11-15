using Microsoft.AspNetCore.Http.HttpResults;

using OpenTicket.Application.Dashboard;

namespace OpenTicket.API.Endpoints;

internal static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/dashboard/summary", GetSummaryAsync)
            .WithTags("Dashboard")
            .WithName("GetDashboardSummary")
            .WithSummary("Ticket counts in the caller's scope.");
        return api;
    }

    private static async Task<Ok<DashboardSummary>> GetSummaryAsync(DashboardService dashboard, CancellationToken ct) =>
        TypedResults.Ok(await dashboard.GetSummaryAsync(ct));
}
