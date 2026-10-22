using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Contracts;
using RaceDay.Data;
using RaceDay.Models;
using Xunit;

namespace RaceDay.Api.Tests;

public class AuthenticationTests
{
    private const string TestEmail = "test.runner@example.com";
    private const string TestPassword = "TestRunner@2026";

    [Fact]
    public async Task Register_ValidRequest_CreatesUserAndHashesPassword()
    {
        using var factory = new RaceDayWebApplicationFactory();
        using var client = factory.CreateSessionClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            ValidRegistration());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var account = await response.Content
            .ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(account);
        Assert.True(account!.UserId > 0);
        Assert.Equal(TestEmail, account.Email);
        Assert.Equal("Participant", account.Role);

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RaceDayDbContext>();

        var hasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<AppUser>>();

        var savedUser = await db.Users
            .SingleAsync(u => u.Email == TestEmail);

        Assert.NotEqual(TestPassword, savedUser.PasswordHash);

        var verification = hasher.VerifyHashedPassword(
            savedUser,
            savedUser.PasswordHash,
            TestPassword);

        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            verification);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        using var factory = new RaceDayWebApplicationFactory();
        using var client = factory.CreateSessionClient();

        await RegisterUser(client);

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            ValidRegistration());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("short", 2)]
    [InlineData(TestPassword, 3)]
    public async Task Register_InvalidInput_ReturnsBadRequest(
        string password,
        int roleId)
    {
        using var factory = new RaceDayWebApplicationFactory();
        using var client = factory.CreateSessionClient();

        var request = ValidRegistration();
        request.Password = password;
        request.RoleId = roleId;

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RaceDayDbContext>();

        Assert.False(await db.Users.AnyAsync());
    }

    [Fact]
    public async Task Login_ValidCredentials_PersistsSession()
    {
        using var factory = new RaceDayWebApplicationFactory();
        using var client = factory.CreateSessionClient();

        await RegisterUser(client);
        await LoginUser(client);

        var response = await client.GetAsync("/api/auth/session");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var account = await response.Content
            .ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(account);
        Assert.Equal(TestEmail, account!.Email);
        Assert.Equal("Participant", account.Role);

        // A separate client must not inherit this user's session.
        using var otherClient = factory.CreateSessionClient();

        var otherResponse = await otherClient.GetAsync(
            "/api/auth/session");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            otherResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        using var factory = new RaceDayWebApplicationFactory();
        using var client = factory.CreateSessionClient();

        await RegisterUser(client);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Email = TestEmail,
                Password = "WrongPassword@2026"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var sessionResponse = await client.GetAsync(
            "/api/auth/session");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            sessionResponse.StatusCode);
    }

    [Fact]
    public async Task Profile_WithoutLogin_ReturnsUnauthorized()
    {
        using var factory = new RaceDayWebApplicationFactory();
        using var client = factory.CreateSessionClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_AfterLogin_ReturnsOwnAccount()
    {
        using var factory = new RaceDayWebApplicationFactory();
        using var client = factory.CreateSessionClient();

        await RegisterUser(client);
        await LoginUser(client);

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content
            .ReadFromJsonAsync<ProfileDto>();

        Assert.NotNull(profile);
        Assert.Equal(TestEmail, profile!.Email);
        Assert.Equal("Participant", profile.Role);
    }

    [Fact]
    public async Task Logout_ClearsSessionAndBlocksProfile()
    {
        using var factory = new RaceDayWebApplicationFactory();
        using var client = factory.CreateSessionClient();

        await RegisterUser(client);
        await LoginUser(client);

        var logoutResponse = await client.PostAsync(
            "/api/auth/logout",
            null);

        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        var sessionResponse = await client.GetAsync(
            "/api/auth/session");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            sessionResponse.StatusCode);

        var profileResponse = await client.GetAsync("/api/users/me");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            profileResponse.StatusCode);
    }

    private static RegisterRequest ValidRegistration()
    {
        return new RegisterRequest
        {
            FirstName = "Test",
            LastName = "Runner",
            Email = TestEmail,
            Password = TestPassword,
            RoleId = 2
        };
    }

    private static async Task RegisterUser(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            ValidRegistration());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task LoginUser(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Email = TestEmail,
                Password = TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}