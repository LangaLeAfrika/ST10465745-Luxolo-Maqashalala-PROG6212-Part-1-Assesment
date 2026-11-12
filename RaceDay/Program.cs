using Microsoft.EntityFrameworkCore;
using RaceDay.Data;
using Microsoft.AspNetCore.Identity;
using RaceDay.Models;

var builder = WebApplication.CreateBuilder(args);

// Explicit opt-in for the local Docker Desktop demo; never allowed in production.
var localDockerHttp = builder.Configuration.GetValue<bool>("LocalDockerDemo:AllowHttp");
if (localDockerHttp && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("LocalDockerDemo:AllowHttp requires Development.");
}
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddScoped<RaceDay.Services.IImageStorage, RaceDay.Services.AzureImageStorage>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<
    IPasswordHasher<AppUser>,
    PasswordHasher<AppUser>>();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);

    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = localDockerHttp
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "RaceDay API");
    });
}

if (!localDockerHttp)
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllers();

app.Run();
public partial class Program
{
}
