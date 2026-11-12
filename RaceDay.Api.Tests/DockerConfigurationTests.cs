using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RaceDay.Api.Tests;

public class DockerConfigurationTests
{
    private sealed class ApiFactory(bool allowHttp, string environment = "Development") : RaceDayWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment(environment);
            builder.UseSetting("LocalDockerDemo:AllowHttp", allowHttp.ToString());
            builder.UseSetting("https_port", "7259");
        }
    }

    [Fact]
    public async Task ApiDemo_HttpLoginRetainsSessionForProtectedRequest()
    {
        using var app = new ApiFactory(true);
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        using var register = await client.PostAsJsonAsync("/api/auth/register", new
        { firstName = "Docker", lastName = "Runner", email = "docker@example.com", password = "DockerTests@2026", roleId = 2 });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new
        { email = "docker@example.com", password = "DockerTests@2026" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = string.Join(";", login.Headers.GetValues("Set-Cookie"));
        Assert.DoesNotContain("; secure", cookie.ToLowerInvariant());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task NormalApi_StillRedirectsHttpToHttps()
    {
        using var app = new ApiFactory(false);
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/events");
        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal("https", response.Headers.Location!.Scheme);
    }

    [Fact]
    public void ApiDemo_CannotBeEnabledInProduction()
    {
        using var app = new ApiFactory(true, "Production");
        var error = Assert.Throws<InvalidOperationException>(() => app.CreateClient());
        Assert.Contains("requires Development", error.Message);
    }

    private sealed class MvcFactory(bool allowHttp, string environment = "Development")
        : WebApplicationFactory<RaceDay.Mvc.Controllers.HomeController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("LocalDockerDemo:AllowHttp", allowHttp.ToString());
            builder.UseSetting("https_port", "7266");
        }
    }

    [Theory]
    [InlineData(true, HttpStatusCode.OK)]
    [InlineData(false, HttpStatusCode.TemporaryRedirect)]
    public async Task Mvc_HttpHomeRequiresExplicitDemoMode(bool enabled, HttpStatusCode expected)
    {
        using var app = new MvcFactory(enabled);
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
        Assert.Equal(expected, (await client.GetAsync("/")).StatusCode);
    }

    [Fact]
    public void MvcDemo_CannotBeEnabledInProduction()
    {
        using var app = new MvcFactory(true, "Production");
        var error = Assert.Throws<InvalidOperationException>(() => app.CreateClient());
        Assert.Contains("requires Development", error.Message);
    }
}
