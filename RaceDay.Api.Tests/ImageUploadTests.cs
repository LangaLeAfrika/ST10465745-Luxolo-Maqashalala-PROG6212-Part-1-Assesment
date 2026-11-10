using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaceDay.Contracts;
using RaceDay.Data;
using RaceDay.Models;
using RaceDay.Services;
using Xunit;

namespace RaceDay.Api.Tests;

public class ImageUploadTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a6n8AAAAASUVORK5CYII=");

    private sealed class MemoryImages : IImageStorage
    {
        public readonly ConcurrentDictionary<string, StoredImage> Images = new();
        public bool Fail { get; set; }
        public Task UploadAsync(string path, byte[] bytes, string type, CancellationToken ct)
        {
            if (Fail) throw new ImageStorageUnavailableException();
            Images[path] = new(bytes, type); return Task.CompletedTask;
        }
        public Task<StoredImage?> ReadAsync(string path, CancellationToken ct) =>
            Task.FromResult(Images.GetValueOrDefault(path));
        public Task DeleteAsync(string path, CancellationToken ct)
        { Images.TryRemove(path, out _); return Task.CompletedTask; }
    }

    private sealed class ImageFactory : RaceDayWebApplicationFactory
    {
        public readonly MemoryImages Store = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IImageStorage>();
                services.AddSingleton<IImageStorage>(Store);
            });
        }
    }

    private static MultipartFormDataContent File(byte[] bytes, string name = "photo.png")
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", name);
        return form;
    }

    private static async Task<int> Login(HttpClient client, int role = 2)
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        using var registered = await client.PostAsJsonAsync("/api/auth/register", new
        { firstName = "Image", lastName = "Tester", email, password = "ImageTests@2026", roleId = role });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var account = await registered.Content.ReadFromJsonAsync<AuthResponse>();
        using var loggedIn = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "ImageTests@2026" });
        Assert.Equal(HttpStatusCode.OK, loggedIn.StatusCode);
        return account!.UserId;
    }

    [Fact]
    public async Task ProfilePhoto_RoundTrip_Replacement_AndOtherUserIsolation()
    {
        using var factory = new ImageFactory();
        using var client = factory.CreateSessionClient();
        var userId = await Login(client);
        using (var form = File(Png))
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/users/me/photo", form)).StatusCode);
        using var first = await client.GetAsync("/api/users/me/photo");
        Assert.Equal(Png, await first.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/png", first.Content.Headers.ContentType!.MediaType);
        using (var form = File(Png))
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/users/me/photo", form)).StatusCode);
        Assert.Single(factory.Store.Images);
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<RaceDayDbContext>().Users.FindAsync(userId);
        Assert.StartsWith($"users/{userId}/", user!.ProfileImagePath);
        using var other = factory.CreateSessionClient();
        await Login(other);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/users/me/photo")).StatusCode);
    }

    [Theory]
    [InlineData("fake.png")]
    [InlineData("unsafe.svg")]
    [InlineData("photo.html")]
    public async Task InvalidImage_IsRejected(string filename)
    {
        using var factory = new ImageFactory();
        using var client = factory.CreateSessionClient();
        await Login(client);
        using var form = File("<script>alert(1)</script>"u8.ToArray(), filename);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/users/me/photo", form)).StatusCode);
        Assert.Empty(factory.Store.Images);
    }

    [Fact]
    public async Task OversizeImage_IsRejected()
    {
        using var factory = new ImageFactory();
        using var client = factory.CreateSessionClient();
        await Login(client);
        using var form = File(new byte[5 * 1024 * 1024 + 1]);
        var response = await client.PostAsync("/api/users/me/photo", form);
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge);
        Assert.Empty(factory.Store.Images);
    }

    [Fact]
    public async Task Banner_OnlyOwnerCanUpload_AndPublicCanView()
    {
        using var factory = new ImageFactory();
        using var owner = factory.CreateSessionClient();
        using var other = factory.CreateSessionClient();
        using var participant = factory.CreateSessionClient();
        var ownerId = await Login(owner, 1);
        await Login(other, 1);
        await Login(participant);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var race = new Event { EventName = "Image race", Location = "Kouga", Distance = 5,
            EventType = "Running", EventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)), OrganiserId = ownerId };
        db.Events.Add(race); await db.SaveChangesAsync();
        var route = $"/api/events/{race.EventId}/banner";
        using (var form = File(Png))
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync(route, form)).StatusCode);
        using (var form = File(Png))
            Assert.Equal(HttpStatusCode.Forbidden, (await participant.PostAsync(route, form)).StatusCode);
        Assert.Empty(factory.Store.Images);
        using (var form = File(Png))
            Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync(route, form)).StatusCode);
        using var anonymous = factory.CreateSessionClient();
        Assert.Equal(Png, await anonymous.GetByteArrayAsync(route));
    }

    [Fact]
    public async Task AnonymousCannotUploadOrReadPrivatePhoto()
    {
        using var factory = new ImageFactory();
        using var client = factory.CreateSessionClient();
        using var form = File(Png);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/users/me/photo", form)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me/photo")).StatusCode);
    }

    [Fact]
    public async Task StorageFailure_DoesNotSaveDatabasePath()
    {
        using var factory = new ImageFactory();
        using var client = factory.CreateSessionClient();
        var userId = await Login(client);
        factory.Store.Fail = true;
        using var form = File(Png);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync("/api/users/me/photo", form)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<RaceDayDbContext>().Users.FindAsync(userId);
        Assert.Null(user!.ProfileImagePath);
    }
}
