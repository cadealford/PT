using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PetroTransit.API.Models;

namespace PetroTransit.API.Tests;

internal static class ApiTestHelpers
{
    public static async Task<string> RegisterAndLoginAsync(
        HttpClient client,
        string username,
        string email,
        string password,
        string fullName = "Test User")
    {
        var registerResponse = await client.PostAsJsonAsync("/api/Auth/register", new
        {
            username,
            email,
            password,
            fullName
        });
        registerResponse.EnsureSuccessStatusCode();

        return await LoginAsync(client, username, password);
    }

    public static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/Auth/login", new
        {
            username,
            password
        });
        loginResponse.EnsureSuccessStatusCode();

        var body = await loginResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("token").GetString()
               ?? throw new InvalidOperationException("Token was missing from login response.");
    }

    public static void SetBearerToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task SeedAdminAsync(
        TestWebAppFactory factory,
        string username,
        string email,
        string password,
        string fullName = "Admin User")
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = new AppUser
        {
            UserName = username,
            Email = email,
            FullName = fullName,
            IsAdmin = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to seed admin user: {errors}");
        }
    }
}
