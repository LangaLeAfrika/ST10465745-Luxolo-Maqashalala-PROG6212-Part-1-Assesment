using System.Net;
using System.Net.Http.Json;
using RaceDay.Contracts;
using Xunit;

namespace RaceDay.Api.Tests;

public class CategoryAndResultTests : IDisposable
{
    private readonly RaceDayWebApplicationFactory _factory = new();
    private readonly HttpClient _organiser;
    private readonly HttpClient _participant;
    private readonly HttpClient _otherUser;

    public CategoryAndResultTests()
    {
        _organiser = _factory.CreateSessionClient();
        _participant = _factory.CreateSessionClient();
        _otherUser = _factory.CreateSessionClient();
    }

    [Theory]
    [InlineData(-1, 30)]
    [InlineData(18, -1)]
    [InlineData(30, 18)]
    public async Task Category_RejectsInvalidAgeLimits(
        int minimumAge, int maximumAge)
    {
        var scenario = await CreateScenario();

        var response = await _organiser.PostAsJsonAsync(
            $"/api/events/{scenario.EventId}/categories",
            new
            {
                categoryName = "Invalid category",
                minimumAge,
                maximumAge
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Category_RejectsDuplicateNameWithinEvent()
    {
        var scenario = await CreateScenario();

        var response = await _organiser.PostAsJsonAsync(
            $"/api/events/{scenario.EventId}/categories",
            new
            {
                categoryName = "Open",
                minimumAge = 18
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(1)] // Another organiser
    [InlineData(2)] // A participant
    public async Task Category_RejectsChangesByUnauthorisedUser(int roleId)
    {
        var scenario = await CreateScenario();
        await RegisterAndLogin(_otherUser, roleId);

        var create = await _otherUser.PostAsJsonAsync(
            $"/api/events/{scenario.EventId}/categories",
            new { categoryName = "Unauthorised category" });

        var update = await _otherUser.PutAsJsonAsync(
            $"/api/categories/{scenario.CategoryId}",
            new { categoryName = "Unauthorised edit" });

        var delete = await _otherUser.DeleteAsync(
            $"/api/categories/{scenario.CategoryId}");

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);

        var category = await Read<CategoryDto>(
            await _organiser.GetAsync(
                $"/api/categories/{scenario.CategoryId}"),
            HttpStatusCode.OK);

        Assert.Equal("Open", category.CategoryName);
    }

    [Fact]
    public async Task Category_WithEnrolment_CannotBeDeleted()
    {
        var scenario = await CreateScenario();

        var response = await _organiser.DeleteAsync(
            $"/api/categories/{scenario.CategoryId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Result_OwnerCanCaptureAndUpdate_ParticipantSeesOwnResult()
    {
        var scenario = await CreateScenario();

        var created = await Read<ResultDto>(
            await _organiser.PostAsJsonAsync(
                $"/api/enrolments/{scenario.EnrolmentId}/result",
                new
                {
                    finishTime = TimeSpan.FromMinutes(48),
                    finishingPosition = 3
                }),
            HttpStatusCode.Created);

        Assert.Equal(scenario.EnrolmentId, created.EnrolmentId);
        Assert.Equal(TimeSpan.FromMinutes(48), created.FinishTime);
        Assert.Equal(3, created.FinishingPosition);

        var duplicate = await _organiser.PostAsJsonAsync(
            $"/api/enrolments/{scenario.EnrolmentId}/result",
            new
            {
                finishTime = TimeSpan.FromMinutes(49),
                finishingPosition = 4
            });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var updated = await Read<ResultDto>(
            await _organiser.PutAsJsonAsync(
                $"/api/results/{created.ResultId}",
                new
                {
                    finishTime = TimeSpan.FromMinutes(47),
                    finishingPosition = 2
                }),
            HttpStatusCode.OK);

        Assert.Equal(TimeSpan.FromMinutes(47), updated.FinishTime);
        Assert.Equal(2, updated.FinishingPosition);

        var myResults = await Read<List<ResultDto>>(
            await _participant.GetAsync("/api/results/me"),
            HttpStatusCode.OK);

        var myResult = Assert.Single(myResults);

        Assert.Equal(created.ResultId, myResult.ResultId);
        Assert.Equal(TimeSpan.FromMinutes(47), myResult.FinishTime);
        Assert.Equal(2, myResult.FinishingPosition);

        // Another participant must not receive this person's result.
        await RegisterAndLogin(_otherUser, 2);

        var otherResults = await Read<List<ResultDto>>(
            await _otherUser.GetAsync("/api/results/me"),
            HttpStatusCode.OK);

        Assert.Empty(otherResults);
    }

    [Theory]
    [InlineData(1)] // Another organiser
    [InlineData(2)] // A participant
    public async Task Result_RejectsUnauthorisedCaptureUpdateAndEventListing(
        int roleId)
    {
        var scenario = await CreateScenario();
        await RegisterAndLogin(_otherUser, roleId);

        var payload = new
        {
            finishTime = TimeSpan.FromMinutes(48),
            finishingPosition = 3
        };

        var deniedCreate = await _otherUser.PostAsJsonAsync(
            $"/api/enrolments/{scenario.EnrolmentId}/result",
            payload);

        Assert.Equal(HttpStatusCode.Forbidden, deniedCreate.StatusCode);

        var result = await Read<ResultDto>(
            await _organiser.PostAsJsonAsync(
                $"/api/enrolments/{scenario.EnrolmentId}/result",
                payload),
            HttpStatusCode.Created);

        var deniedUpdate = await _otherUser.PutAsJsonAsync(
            $"/api/results/{result.ResultId}",
            new
            {
                finishTime = TimeSpan.FromMinutes(1),
                finishingPosition = 1
            });

        Assert.Equal(HttpStatusCode.Forbidden, deniedUpdate.StatusCode);

        var deniedListing = await _otherUser.GetAsync(
            $"/api/events/{scenario.EventId}/results");

        Assert.Equal(HttpStatusCode.Forbidden, deniedListing.StatusCode);

        // Confirm that the rejected update did not change the result.
        var results = await Read<List<ResultDto>>(
            await _organiser.GetAsync(
                $"/api/events/{scenario.EventId}/results"),
            HttpStatusCode.OK);

        var saved = Assert.Single(results);

        Assert.Equal(TimeSpan.FromMinutes(48), saved.FinishTime);
        Assert.Equal(3, saved.FinishingPosition);
    }

    [Theory]
    [InlineData("00:00:00", 1)]
    [InlineData("-00:01:00", 1)]
    [InlineData("1.00:00:00", 1)]
    [InlineData("00:48:00", 0)]
    [InlineData("00:48:00", -1)]
    public async Task Result_RejectsInvalidTimeOrPosition(
        string finishTime, int finishingPosition)
    {
        var scenario = await CreateScenario();

        var response = await _organiser.PostAsJsonAsync(
            $"/api/enrolments/{scenario.EnrolmentId}/result",
            new { finishTime, finishingPosition });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var results = await Read<List<ResultDto>>(
            await _participant.GetAsync("/api/results/me"),
            HttpStatusCode.OK);

        Assert.Empty(results);
    }

    [Fact]
    public async Task Result_CannotBeCapturedForFutureEvent()
    {
        var scenario = await CreateScenario(futureEvent: true);

        var response = await _organiser.PostAsJsonAsync(
            $"/api/enrolments/{scenario.EnrolmentId}/result",
            new
            {
                finishTime = TimeSpan.FromMinutes(48),
                finishingPosition = 1
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Results_RequireLogin()
    {
        var response = await _otherUser.GetAsync("/api/results/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<Scenario> CreateScenario(bool futureEvent = false)
    {
        await RegisterAndLogin(_organiser, 1);
        await RegisterAndLogin(_participant, 2);

        var raceEvent = await Read<EventDto>(
            await _organiser.PostAsJsonAsync("/api/events", new
            {
                eventName = "Results test race",
                description = "Automated test event",
                eventDate = DateOnly.FromDateTime(
                    DateTime.UtcNow.AddDays(futureEvent ? 30 : -2)),
                location = "Jeffreys Bay",
                distance = 10m,
                eventType = "Running"
            }),
            HttpStatusCode.Created);

        var category = await Read<CategoryDto>(
            await _organiser.PostAsJsonAsync(
                $"/api/events/{raceEvent.EventId}/categories",
                new
                {
                    categoryName = "Open",
                    minimumAge = 18
                }),
            HttpStatusCode.Created);

        var enrolment = await Read<EnrolmentDto>(
            await _participant.PostAsJsonAsync(
                $"/api/events/{raceEvent.EventId}/enrolments",
                new { categoryId = category.CategoryId }),
            HttpStatusCode.Created);

        return new Scenario(
            raceEvent.EventId,
            category.CategoryId,
            enrolment.EnrolmentId);
    }

    private static async Task RegisterAndLogin(
        HttpClient client, int roleId)
    {
        var email = $"test-{Guid.NewGuid():N}@example.com";
        const string password = "RaceDayTest@2026";

        await Read<AuthResponse>(
            await client.PostAsJsonAsync("/api/auth/register", new
            {
                firstName = "Test",
                lastName = "User",
                email,
                password,
                roleId
            }),
            HttpStatusCode.Created);

        await Read<AuthResponse>(
            await client.PostAsJsonAsync("/api/auth/login", new
            {
                email,
                password
            }),
            HttpStatusCode.OK);
    }

    private static async Task<T> Read<T>(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus) where T : class
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync();

            Assert.True(
                response.StatusCode == expectedStatus,
                $"Expected {(int)expectedStatus}, " +
                $"received {(int)response.StatusCode}. Body: {body}");

            var value = await response.Content.ReadFromJsonAsync<T>();
            Assert.NotNull(value);

            return value!;
        }
    }

    public void Dispose()
    {
        _organiser.Dispose();
        _participant.Dispose();
        _otherUser.Dispose();
        _factory.Dispose();
    }

    private sealed record Scenario(
        int EventId,
        int CategoryId,
        int EnrolmentId);
}