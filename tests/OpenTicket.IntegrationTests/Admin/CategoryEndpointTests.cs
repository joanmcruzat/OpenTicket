using System.Net;
using System.Net.Http.Json;

using OpenTicket.Application.Categories;
using OpenTicket.Domain.Users;
using OpenTicket.IntegrationTests.Infrastructure;

namespace OpenTicket.IntegrationTests.Admin;

[Collection(ApiTestGroup.Name)]
public sealed class CategoryEndpointTests(OpenTicketApiFactory factory)
{
    private static CancellationToken Ct => TestApi.Ct;

    [Fact]
    public async Task Admin_CreatesUpdatesAndDeactivates_CustomerSeesOnlyActive()
    {
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        var name = $"Billing {Guid.NewGuid():N}";

        using var created = await admin.PostAsJsonAsync("/api/v1/categories", new { name, description = "Invoices" }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var category = await created.ReadJsonAsync<CategoryView>();

        using var renamed = await admin.PutAsJsonAsync($"/api/v1/categories/{category.Id}", new { name = name + " & Payments", description = (string?)null }, Ct);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Null((await renamed.ReadJsonAsync<CategoryView>()).Description);

        using var deactivated = await admin.PostAsync(new Uri($"/api/v1/categories/{category.Id}/deactivate", UriKind.Relative), null, Ct);
        Assert.False((await deactivated.ReadJsonAsync<CategoryView>()).IsActive);

        Assert.DoesNotContain(await customer.ListCategoriesAsync("?includeInactive=true"), c => c.Id == category.Id);
        Assert.Contains(await admin.ListCategoriesAsync("?includeInactive=true"), c => c.Id == category.Id);
        Assert.DoesNotContain(await admin.ListCategoriesAsync(""), c => c.Id == category.Id);
    }

    [Fact]
    public async Task Create_DuplicateNameInAnyCase_Returns409()
    {
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);
        var name = $"Hardware {Guid.NewGuid():N}";
        using (await admin.PostAsJsonAsync("/api/v1/categories", new { name }, Ct))
        {
        }

        using var duplicate = await admin.PostAsJsonAsync("/api/v1/categories", new { name = $"  {name.ToUpperInvariant()} " }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("category.duplicate_name", await duplicate.ProblemCodeAsync());
    }

    [Fact]
    public async Task Writes_ByNonAdmin_Return403()
    {
        var category = await factory.CreateCategoryAsync();
        using var agent = await factory.CreateClientForNewUserAsync(UserRole.Agent);

        using var create = await agent.PostAsJsonAsync("/api/v1/categories", new { name = "Nope" }, Ct);
        using var deactivate = await agent.PostAsync(new Uri($"/api/v1/categories/{category.Id}/deactivate", UriKind.Relative), null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deactivate.StatusCode);
    }

    [Fact]
    public async Task Update_UnknownId_Returns404_AndBlankName400()
    {
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);

        using var unknown = await admin.PutAsJsonAsync($"/api/v1/categories/{Guid.NewGuid()}", new { name = "Ghost" }, Ct);
        using var blank = await admin.PostAsJsonAsync("/api/v1/categories", new { name = "   " }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
    }
}

internal static class CategoryApi
{
    public static async Task<List<CategoryView>> ListCategoriesAsync(this HttpClient client, string query)
    {
        using var response = await client.GetAsync(new Uri($"/api/v1/categories{query}", UriKind.Relative), TestApi.Ct);
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync<List<CategoryView>>();
    }
}
