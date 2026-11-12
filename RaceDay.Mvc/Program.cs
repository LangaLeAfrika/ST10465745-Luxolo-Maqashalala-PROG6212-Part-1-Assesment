using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Services;

var builder = WebApplication.CreateBuilder(args);

// Explicit opt-in for the local Docker Desktop demo; never allowed in production.
var localDockerHttp = builder.Configuration.GetValue<bool>("LocalDockerDemo:AllowHttp");
if (localDockerHttp && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("LocalDockerDemo:AllowHttp requires Development.");
}

builder.Services.AddControllersWithViews(options =>
{
    // Protect form submissions against cross-site request forgery.
    options.Filters.Add(
        new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);

    // Different name from the API's session cookie.
    options.Cookie.Name = ".RaceDay.Mvc.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;

    options.Cookie.SecurePolicy =
        builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
});

builder.Services.AddHttpContextAccessor();

builder.Services
    .AddHttpClient("RaceDayApi", client =>
    {
        var baseUrl = builder.Configuration["ApiSettings:BaseUrl"]
            ?? throw new InvalidOperationException(
                "ApiSettings:BaseUrl is missing.");

        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
        new HttpClientHandler
        {
            UseCookies = false,
            AllowAutoRedirect = false
        });

builder.Services.AddScoped<RaceDayApiClient>();

var app = builder.Build();

app.UseExceptionHandler("/Home/Error");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (!localDockerHttp)
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en-GB")
    .AddSupportedCultures("en-GB")
    .AddSupportedUICultures("en-GB"));

app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();