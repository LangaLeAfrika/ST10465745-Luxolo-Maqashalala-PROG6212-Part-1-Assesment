using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Mvc.Controllers;

namespace RaceDay.Api.Tests;

// Real MVC routes, Razor views, session cookies and antiforgery middleware;
// deterministic fake API responses, without LocalDB or a running API process.
public class MvcSmokeTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/Auth/Login")]
    [InlineData("/Auth/Register")]
    public async Task PublicPages_RenderWithMobileNavigation(string path)
    {
        using var app = new MvcFactory();
        using var client = app.Client();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"main-navigation\"", html);
        Assert.Contains("data-bs-target=\"#main-navigation\"", html);
    }

    [Theory]
    [InlineData("/Organiser")]
    [InlineData("/Participant/MyEvents")]
    [InlineData("/Profile")]
    [InlineData("/Images/PhotoUpload")]
    [InlineData("/Images/BannerUpload/1")]
    public async Task ProtectedPages_RedirectAnonymousUsers(string path)
    {
        using var app = new MvcFactory();
        using var client = app.Client();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Auth/Login", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Login_RejectsMissingAntiforgeryToken()
    {
        using var app = new MvcFactory();
        using var client = app.Client();
        var response = await client.PostAsync("/Auth/Login", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["Email"] = "runner@example.com", ["Password"] = "Testing@2026" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/Participant", "Browse events")]
    [InlineData("/Participant/MyEvents", "Confirmed")]
    [InlineData("/Participant/MyResults", "00:45:30")]
    [InlineData("/Profile", "Runner")]
    [InlineData("/Images/PhotoUpload", "multipart/form-data")]
    public async Task ParticipantPages_RenderWithForwardedApiSession(string path, string expected)
    {
        using var app = new MvcFactory();
        using var client = app.Client();
        await Login(client);
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PhotoUpload_RejectsMissingAntiforgeryToken()
    {
        using var app = new MvcFactory();
        using var client = app.Client();
        await Login(client);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(new byte[] { 1, 2 }), "File", "photo.png");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsync("/Images/PhotoUpload", form)).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotOpenOrganiserDashboard()
    {
        using var app = new MvcFactory();
        using var client = app.Client();
        await Login(client);
        var response = await client.GetAsync("/Organiser");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredApiSession_RedirectsToLogin()
    {
        using var app = new MvcFactory();
        using var client = app.Client();
        await Login(client);
        app.Api.Expired = true;
        var response = await client.GetAsync("/Participant/MyEvents");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Auth/Login", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task SeparateBrowser_DoesNotInheritLogin()
    {
        using var app = new MvcFactory();
        using var loggedIn = app.Client();
        using var anonymous = app.Client();
        await Login(loggedIn);
        Assert.Equal(HttpStatusCode.Redirect,
            (await anonymous.GetAsync("/Participant/MyEvents")).StatusCode);
    }

    private static async Task Login(HttpClient client)
    {
        var html = await client.GetStringAsync("/Auth/Login");
        var token = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, "Login form must contain an antiforgery token.");
        var response = await client.PostAsync("/Auth/Login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Email"] = "runner@example.com", ["Password"] = "Testing@2026",
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value)
            }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private sealed class MvcFactory : WebApplicationFactory<HomeController>
    {
        public FakeApi Api { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
                services.AddHttpClient("RaceDayApi", c => c.BaseAddress = new Uri("https://fake-api/"))
                    .ConfigurePrimaryHttpMessageHandler(() => Api));
        }
        public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
        });
    }

    private sealed class FakeApi : HttpMessageHandler
    {
        public bool Expired { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            object identity = new { userId = 1, firstName = "Runner", lastName = "Demo", email = "runner@example.com", role = "Participant" };
            if (path == "/api/auth/login")
            {
                var response = Json(identity);
                response.Headers.Add("Set-Cookie", ".RaceDay.Session=demo; Path=/; HttpOnly");
                return Task.FromResult(response);
            }
            if (Expired || !request.Headers.TryGetValues("Cookie", out var cookies) ||
                !cookies.Contains(".RaceDay.Session=demo"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            object? data = path switch
            {
                "/api/auth/session" or "/api/users/me" => identity,
                "/api/events" => Array.Empty<object>(),
                "/api/enrolments/me" => new[] { new { enrolmentId = 1, eventId = 1, eventName = "Demo race", categoryName = "Open", enrolmentDate = "2026-11-01T10:00:00", status = "Confirmed" } },
                "/api/results/me" => new[] { new { resultId = 1, eventId = 1, eventName = "Demo race", eventDate = "2026-11-01", categoryName = "Open", finishTime = "00:45:30", finishingPosition = 1 } },
                _ => null
            };
            return Task.FromResult(data is null ? new HttpResponseMessage(HttpStatusCode.NotFound) : Json(data));
        }
        private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value)
        };
    }
}
