using Microsoft.Extensions.DependencyInjection;

using OpenTicket.Application.Categories;
using OpenTicket.Application.Comments;
using OpenTicket.Application.Dashboard;
using OpenTicket.Application.Sprints;
using OpenTicket.Application.Tickets;

namespace OpenTicket.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<TicketQueryService>();
        services.AddScoped<TicketCommandService>();
        services.AddScoped<CommentService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<SprintQueries>();
        services.AddScoped<SprintService>();
        return services;
    }
}
