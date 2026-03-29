using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetroTransit.API.Data;
using PetroTransit.API.Models;

namespace PetroTransit.API.Tests;

public class DomainCrudTests
{
    [Fact]
    public async Task Airplanes_AdminCrud_Works_AndNonAdminIsForbidden()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClient();

        await ApiTestHelpers.SeedAdminAsync(factory, "admin_air", "admin_air@example.com", "AdminPass123!");
        await ApiTestHelpers.RegisterAndLoginAsync(client, "user_air", "user_air@example.com", "UserPass123!");

        var userToken = await ApiTestHelpers.LoginAsync(client, "user_air", "UserPass123!");
        client.SetBearerToken(userToken);

        var forbiddenCreate = await client.PostAsJsonAsync("/api/Airplanes", new
        {
            name = "Cessna 172",
            registrationNumber = "N172PT",
            capacity = 4
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);

        var adminToken = await ApiTestHelpers.LoginAsync(client, "admin_air", "AdminPass123!");
        client.SetBearerToken(adminToken);

        var create = await client.PostAsJsonAsync("/api/Airplanes", new
        {
            name = "King Air",
            registrationNumber = "N200PT",
            capacity = 8
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createdPlane = await create.Content.ReadFromJsonAsync<Airplane>();
        Assert.NotNull(createdPlane);

        var update = await client.PutAsJsonAsync($"/api/Airplanes/{createdPlane!.Id}", new
        {
            id = createdPlane.Id,
            name = "King Air 350",
            registrationNumber = "N200PT",
            capacity = 9
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var delete = await client.DeleteAsync($"/api/Airplanes/{createdPlane.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Personnel_AdminCrud_Works_AndNonAdminIsForbidden()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClient();

        await ApiTestHelpers.SeedAdminAsync(factory, "admin_pers", "admin_pers@example.com", "AdminPass123!");
        await ApiTestHelpers.RegisterAndLoginAsync(client, "user_pers", "user_pers@example.com", "UserPass123!");

        var userToken = await ApiTestHelpers.LoginAsync(client, "user_pers", "UserPass123!");
        client.SetBearerToken(userToken);

        var forbiddenCreate = await client.PostAsJsonAsync("/api/Personnel", new { fullName = "Pilot One" });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);

        var adminToken = await ApiTestHelpers.LoginAsync(client, "admin_pers", "AdminPass123!");
        client.SetBearerToken(adminToken);

        var create = await client.PostAsJsonAsync("/api/Personnel", new { fullName = "Pilot Two" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createdPersonnel = await create.Content.ReadFromJsonAsync<Personnel>();
        Assert.NotNull(createdPersonnel);

        var update = await client.PutAsJsonAsync($"/api/Personnel/{createdPersonnel!.Id}", new
        {
            id = createdPersonnel.Id,
            fullName = "Pilot Two Updated"
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var delete = await client.DeleteAsync($"/api/Personnel/{createdPersonnel.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Reservations_CreateAndAuthorizationRules_Work()
    {
        using var factory = new TestWebAppFactory();
        using var client = factory.CreateClient();

        await ApiTestHelpers.SeedAdminAsync(factory, "admin_res", "admin_res@example.com", "AdminPass123!");
        await ApiTestHelpers.RegisterAndLoginAsync(client, "owner_res", "owner_res@example.com", "OwnerPass123!");
        await ApiTestHelpers.RegisterAndLoginAsync(client, "other_res", "other_res@example.com", "OtherPass123!");

        var adminToken = await ApiTestHelpers.LoginAsync(client, "admin_res", "AdminPass123!");
        client.SetBearerToken(adminToken);

        var planeCreate = await client.PostAsJsonAsync("/api/Airplanes", new
        {
            name = "Citation CJ3",
            registrationNumber = "N300PT",
            capacity = 7
        });
        var personnelCreate = await client.PostAsJsonAsync("/api/Personnel", new { fullName = "Captain Test" });
        Assert.Equal(HttpStatusCode.Created, planeCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Created, personnelCreate.StatusCode);

        var plane = await planeCreate.Content.ReadFromJsonAsync<Airplane>();
        var personnel = await personnelCreate.Content.ReadFromJsonAsync<Personnel>();
        Assert.NotNull(plane);
        Assert.NotNull(personnel);

        var ownerToken = await ApiTestHelpers.LoginAsync(client, "owner_res", "OwnerPass123!");
        client.SetBearerToken(ownerToken);

        var reservationStart = DateTime.UtcNow.AddHours(2);
        var reservationEnd = reservationStart.AddHours(2);
        var createReservation = await client.PostAsJsonAsync("/api/Reservations", new
        {
            startTime = reservationStart,
            endTime = reservationEnd,
            airplaneId = plane!.Id,
            personnelId = personnel!.Id,
            location = "KDAL",
            flightDetails = "Local test flight",
            passengerCount = 2,
            notes = "Owner created"
        });
        Assert.Equal(HttpStatusCode.Created, createReservation.StatusCode);
        var reservation = await createReservation.Content.ReadFromJsonAsync<Reservation>();
        Assert.NotNull(reservation);

        var otherToken = await ApiTestHelpers.LoginAsync(client, "other_res", "OtherPass123!");
        client.SetBearerToken(otherToken);

        var forbiddenUpdate = await client.PutAsJsonAsync($"/api/Reservations/{reservation!.Id}", new
        {
            id = reservation.Id,
            startTime = reservationStart.AddDays(1),
            endTime = reservationEnd.AddDays(1),
            airplaneId = plane.Id,
            personnelId = personnel.Id,
            location = "KHOU",
            flightDetails = "Unauthorized update",
            passengerCount = 3,
            notes = "Should be forbidden"
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenUpdate.StatusCode);

        client.SetBearerToken(ownerToken);
        var ownerUpdate = await client.PutAsJsonAsync($"/api/Reservations/{reservation.Id}", new
        {
            id = reservation.Id,
            startTime = reservationStart.AddHours(1),
            endTime = reservationEnd.AddHours(1),
            airplaneId = plane.Id,
            personnelId = personnel.Id,
            location = "KDAL",
            flightDetails = "Owner update",
            passengerCount = 3,
            notes = "Owner edit"
        });
        Assert.Equal(HttpStatusCode.NoContent, ownerUpdate.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservation.Id);
        Assert.Equal("Owner update", updated.FlightDetails);

        client.SetBearerToken(otherToken);
        var forbiddenDelete = await client.DeleteAsync($"/api/Reservations/{reservation.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);

        client.SetBearerToken(ownerToken);
        var ownerDelete = await client.DeleteAsync($"/api/Reservations/{reservation.Id}");
        Assert.Equal(HttpStatusCode.NoContent, ownerDelete.StatusCode);
    }
}
