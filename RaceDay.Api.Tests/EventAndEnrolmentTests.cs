using System.Net;
using System.Net.Http.Json;
using RaceDay.Contracts;
using Xunit;

namespace RaceDay.Api.Tests;

public class EventAndEnrolmentTests
{
    private const string Password = "Testing@2026";

    [Fact]
    public async Task CreateEvent_WithoutLogin_ReturnsUnauthorized()
    {
        using var factory = new RaceDayWebApplicationFactory();
        using var client = factory.CreateSessionClient();

        var response = await client.PostAsJsonAsync(
            "/api/events",
            EventRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_AsParticipant_ReturnsForbidden()
    {
        using var factory = new RaceDayWebApplicationFactory();
        using var client = factory.CreateSessionClient();

        await RegisterAndLogin(client, "runner@example.com", 2);

        var response = await client.PostAsJsonAsync(
            "/api/events",
            EventRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_AsOrganiser_SavesEventWithCorrectOwner()
    {
        using var factory = new RaceDayWebApplicationFactory();
        using var client = factory.CreateSessionClient();

        var organiser = await RegisterAndLogin(
            client, "owner@example.com", 1);

        var raceEvent = await CreateEvent(client);

        Assert.Equal(organiser.UserId, raceEvent.OrganiserId);
        Assert.Equal(10.5m, raceEvent.Distance);

        var response = await client.GetAsync(
            $"/api/events/{raceEvent.EventId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var savedEvent = await response.Content
            .ReadFromJsonAsync<EventDto>();

        Assert.NotNull(savedEvent);
        Assert.Equal(raceEvent.EventId, savedEvent!.EventId);
        Assert.Equal("Test Community Run", savedEvent.EventName);
    }

    [Fact]
    public async Task OtherOrganiser_CannotUpdateOrDeleteEvent()
    {
        using var factory = new RaceDayWebApplicationFactory();
        using var owner = factory.CreateSessionClient();
        using var other = factory.CreateSessionClient();

        await RegisterAndLogin(owner, "owner@example.com", 1);
        await RegisterAndLogin(other, "other@example.com", 1);

        var raceEvent = await CreateEvent(owner);

        var updateResponse = await other.PutAsJsonAsync(
            $"/api/events/{raceEvent.EventId}",
            EventRequest("Unauthorised change"));

        Assert.Equal(
            HttpStatusCode.Forbidden,
            updateResponse.StatusCode);

        var deleteResponse = await other.DeleteAsync(
            $"/api/events/{raceEvent.EventId}");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            deleteResponse.StatusCode);

        var unchanged = await owner.GetFromJsonAsync<EventDto>(
            $"/api/events/{raceEvent.EventId}");

        Assert.NotNull(unchanged);
        Assert.Equal("Test Community Run", unchanged!.EventName);
    }

    [Fact]
    public async Task Owner_CanUpdateAndDeleteEventWithoutEnrolments()
    {
        using var factory = new RaceDayWebApplicationFactory();
        using var owner = factory.CreateSessionClient();

        await RegisterAndLogin(owner, "owner@example.com", 1);

        var raceEvent = await CreateEvent(owner);

        var updateResponse = await owner.PutAsJsonAsync(
            $"/api/events/{raceEvent.EventId}",
            EventRequest("Updated Community Run"));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await owner.GetFromJsonAsync<EventDto>(
            $"/api/events/{raceEvent.EventId}");

        Assert.NotNull(updated);
        Assert.Equal("Updated Community Run", updated!.EventName);

        var deleteResponse = await owner.DeleteAsync(
            $"/api/events/{raceEvent.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await owner.GetAsync(
            $"/api/events/{raceEvent.EventId}");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Participant_CanEnrol_AndDuplicateEntryIsRejected()
    {
        using var factory = new RaceDayWebApplicationFactory();
        using var owner = factory.CreateSessionClient();
        using var runner = factory.CreateSessionClient();

        await RegisterAndLogin(owner, "owner@example.com", 1);
        var participant = await RegisterAndLogin(
            runner, "runner@example.com", 2);

        var raceEvent = await CreateEvent(owner);
        var category = await CreateCategory(owner, raceEvent.EventId);

        var request = new CreateEnrolmentRequest
        {
            CategoryId = category.CategoryId
        };

        var response = await runner.PostAsJsonAsync(
            $"/api/events/{raceEvent.EventId}/enrolments",
            request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var enrolment = await response.Content
            .ReadFromJsonAsync<EnrolmentDto>();

        Assert.NotNull(enrolment);
        Assert.Equal(participant.UserId, enrolment!.ParticipantId);
        Assert.Equal(raceEvent.EventId, enrolment.EventId);
        Assert.Equal(category.CategoryId, enrolment.CategoryId);

        var duplicateResponse = await runner.PostAsJsonAsync(
            $"/api/events/{raceEvent.EventId}/enrolments",
            request);

        Assert.Equal(
            HttpStatusCode.Conflict,
            duplicateResponse.StatusCode);

        var entries = await runner.GetFromJsonAsync<List<EnrolmentDto>>(
            "/api/enrolments/me");

        Assert.NotNull(entries);
        Assert.Single(entries!);
    }

    [Fact]
    public async Task Enrolment_CategoryFromAnotherEvent_ReturnsBadRequest()
    {
        using var factory = new RaceDayWebApplicationFactory();
        using var owner = factory.CreateSessionClient();
        using var runner = factory.CreateSessionClient();

        await RegisterAndLogin(owner, "owner@example.com", 1);
        await RegisterAndLogin(runner, "runner@example.com", 2);

        var firstEvent = await CreateEvent(owner);
        var secondEvent = await CreateEvent(owner);

        var category = await CreateCategory(
            owner, secondEvent.EventId);

        var response = await runner.PostAsJsonAsync(
            $"/api/events/{firstEvent.EventId}/enrolments",
            new CreateEnrolmentRequest
            {
                CategoryId = category.CategoryId
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var entries = await runner.GetFromJsonAsync<List<EnrolmentDto>>(
            "/api/enrolments/me");

        Assert.NotNull(entries);
        Assert.Empty(entries!);
    }

    [Fact]
    public async Task Organiser_CannotUseParticipantEnrolmentEndpoint()
    {
        using var factory = new RaceDayWebApplicationFactory();
        using var owner = factory.CreateSessionClient();

        await RegisterAndLogin(owner, "owner@example.com", 1);

        var raceEvent = await CreateEvent(owner);
        var category = await CreateCategory(owner, raceEvent.EventId);

        var response = await owner.PostAsJsonAsync(
            $"/api/events/{raceEvent.EventId}/enrolments",
            new CreateEnrolmentRequest
            {
                CategoryId = category.CategoryId
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Enrolment_IsPrivate_AndBlocksEventDeletion()
    {
        using var factory = new RaceDayWebApplicationFactory();
        using var owner = factory.CreateSessionClient();
        using var runner = factory.CreateSessionClient();
        using var otherRunner = factory.CreateSessionClient();
        using var otherOrganiser = factory.CreateSessionClient();

        await RegisterAndLogin(owner, "owner@example.com", 1);
        await RegisterAndLogin(runner, "runner@example.com", 2);
        await RegisterAndLogin(otherRunner, "other.runner@example.com", 2);
        await RegisterAndLogin(otherOrganiser, "other.owner@example.com", 1);

        var raceEvent = await CreateEvent(owner);
        var category = await CreateCategory(owner, raceEvent.EventId);

        var createResponse = await runner.PostAsJsonAsync(
            $"/api/events/{raceEvent.EventId}/enrolments",
            new CreateEnrolmentRequest
            {
                CategoryId = category.CategoryId
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var enrolment = await createResponse.Content
            .ReadFromJsonAsync<EnrolmentDto>();

        Assert.NotNull(enrolment);

        var ownEventEntries = await owner
            .GetFromJsonAsync<List<EnrolmentDto>>(
                $"/api/events/{raceEvent.EventId}/enrolments");

        Assert.NotNull(ownEventEntries);
        Assert.Single(ownEventEntries!);

        var forbiddenView = await otherOrganiser.GetAsync(
            $"/api/events/{raceEvent.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenView.StatusCode);

        var otherEntries = await otherRunner
            .GetFromJsonAsync<List<EnrolmentDto>>("/api/enrolments/me");

        Assert.NotNull(otherEntries);
        Assert.Empty(otherEntries!);

        var forbiddenDelete = await otherRunner.DeleteAsync(
            $"/api/enrolments/{enrolment!.EnrolmentId}");

        Assert.Equal(HttpStatusCode.NotFound, forbiddenDelete.StatusCode);

        var blockedEventDelete = await owner.DeleteAsync(
            $"/api/events/{raceEvent.EventId}");

        Assert.Equal(
            HttpStatusCode.Conflict,
            blockedEventDelete.StatusCode);

        var withdrawal = await runner.DeleteAsync(
            $"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.NoContent, withdrawal.StatusCode);

        var remaining = await runner.GetFromJsonAsync<List<EnrolmentDto>>(
            "/api/enrolments/me");

        Assert.NotNull(remaining);
        Assert.Empty(remaining!);
    }

    private static CreateEventRequest EventRequest(
        string name = "Test Community Run")
    {
        return new CreateEventRequest
        {
            EventName = name,
            Description = "An event created by an automated test.",
            EventDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            Location = "Gqeberha",
            Distance = 10.5m,
            EventType = "Running"
        };
    }

    private static async Task<AuthResponse> RegisterAndLogin(
        HttpClient client,
        string email,
        int roleId)
    {
        var registration = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest
            {
                FirstName = "Test",
                LastName = "User",
                Email = email,
                Password = Password,
                RoleId = roleId
            });

        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Email = email,
                Password = Password
            });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var account = await login.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(account);
        return account!;
    }

    private static async Task<EventDto> CreateEvent(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/events",
            EventRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var raceEvent = await response.Content.ReadFromJsonAsync<EventDto>();

        Assert.NotNull(raceEvent);
        return raceEvent!;
    }

    private static async Task<CategoryDto> CreateCategory(
        HttpClient client,
        int eventId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/events/{eventId}/categories",
            new CreateCategoryRequest
            {
                CategoryName = "Open",
                MinimumAge = 18,
                MaximumAge = null
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();

        Assert.NotNull(category);
        return category!;
    }
}