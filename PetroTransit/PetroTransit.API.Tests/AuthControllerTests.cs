using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PetroTransit.API.Data;
using PetroTransit.API.Models;

namespace PetroTransit.API.Tests;

public class AuthControllerTests
{
    [Fact]
    public async Task ForgotAndResetPasswordFlow_Works_ForExistingUser()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClient();

        var username = "reset_user";
        var email = "reset_user@example.com";
        var oldPassword = "StartPass123!";
        var newPassword = "NewPass123!";

        await ApiTestHelpers.RegisterAndLoginAsync(client, username, email, oldPassword);

        var forgotUsernameResponse = await client.PostAsJsonAsync("/api/Auth/forgot-username", new { email });
        Assert.Equal(HttpStatusCode.OK, forgotUsernameResponse.StatusCode);

        var forgotPasswordResponse = await client.PostAsJsonAsync("/api/Auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.OK, forgotPasswordResponse.StatusCode);

        string resetToken;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            resetToken = await userManager.GeneratePasswordResetTokenAsync(user!);
        }

        var resetResponse = await client.PostAsJsonAsync("/api/Auth/reset-password", new
        {
            email,
            token = resetToken,
            newPassword = newPassword
        });
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var reloginResponse = await client.PostAsJsonAsync("/api/Auth/login", new
        {
            username,
            password = newPassword
        });
        Assert.Equal(HttpStatusCode.OK, reloginResponse.StatusCode);
    }

    [Fact]
    public async Task TestEmailEndpoint_ReturnsOk()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Auth/test-email", new
        {
            toEmail = "someone@example.com",
            subject = "Test",
            body = "Hello"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UsersEndpoint_RequiresAdmin_AndAdminCanPromoteAndDelete()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClient();

        var nonAdminToken = await ApiTestHelpers.RegisterAndLoginAsync(
            client,
            username: "basic_user",
            email: "basic_user@example.com",
            password: "BasicPass123!");

        client.SetBearerToken(nonAdminToken);
        var forbiddenUsersResponse = await client.GetAsync("/api/Auth/users");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenUsersResponse.StatusCode);

        await ApiTestHelpers.SeedAdminAsync(
            factory,
            username: "admin_user",
            email: "admin_user@example.com",
            password: "AdminPass123!");

        var adminToken = await ApiTestHelpers.LoginAsync(client, "admin_user", "AdminPass123!");
        client.SetBearerToken(adminToken);

        var usersResponse = await client.GetAsync("/api/Auth/users");
        Assert.Equal(HttpStatusCode.OK, usersResponse.StatusCode);

        string basicUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            basicUserId = db.Users.Single(u => u.UserName == "basic_user").Id;
        }

        var promoteResponse = await client.PutAsync($"/api/Auth/promote/{basicUserId}", content: null);
        Assert.Equal(HttpStatusCode.OK, promoteResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = db.Users.Single(u => u.Id == basicUserId);
            Assert.True(user.IsAdmin);
        }

        var deleteResponse = await client.DeleteAsync($"/api/Auth/delete/{basicUserId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }
}
