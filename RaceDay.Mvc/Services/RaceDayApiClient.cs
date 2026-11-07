using System.Net.Http.Json;

namespace RaceDay.Mvc.Services;

public class RaceDayApiClient
{
    private const string ApiCookieName = ".RaceDay.Session";
    private const string CookieSessionKey = "ApiSessionCookie";

    private readonly IHttpClientFactory _factory;
    private readonly IHttpContextAccessor _accessor;

    public RaceDayApiClient(
        IHttpClientFactory factory,
        IHttpContextAccessor accessor)
    {
        _factory = factory;
        _accessor = accessor;
    }

    private ISession CurrentSession =>
        _accessor.HttpContext?.Session
        ?? throw new InvalidOperationException(
            "An active MVC request is required.");

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        // Accept only relative API paths.
        if (Uri.TryCreate(url, UriKind.Absolute, out _) ||
            url.StartsWith('/') ||
            url.Contains('\\'))
        {
            throw new ArgumentException(
                "Use a relative API path, such as api/events.",
                nameof(url));
        }

        await CurrentSession.LoadAsync(cancellationToken);

        using var client = _factory.CreateClient("RaceDayApi");
        using var request = new HttpRequestMessage(method, url);

        var cookie = CurrentSession.GetString(CookieSessionKey);

        if (!string.IsNullOrWhiteSpace(cookie))
        {
            request.Headers.Add("Cookie", cookie);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        var response = await client.SendAsync(
            request, cancellationToken);

        CaptureSessionCookie(response);

        // The calling controller must dispose the response.
        return response;
    }

    public async Task<T?> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(
            cancellationToken: cancellationToken);
    }

    private void CaptureSessionCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(
                "Set-Cookie", out var cookieHeaders))
        {
            return;
        }

        foreach (var header in cookieHeaders)
        {
            var nameAndValue = header.Split(';', 2)[0].Trim();

            if (!nameAndValue.StartsWith(
                    $"{ApiCookieName}=",
                    StringComparison.Ordinal))
            {
                continue;
            }

            var value = nameAndValue[(ApiCookieName.Length + 1)..];

            if (string.IsNullOrEmpty(value))
            {
                CurrentSession.Remove(CookieSessionKey);
            }
            else
            {
                CurrentSession.SetString(
                    CookieSessionKey, nameAndValue);
            }
        }
    }

    public void ClearApiSession()
    {
        CurrentSession.Remove(CookieSessionKey);
    }
}