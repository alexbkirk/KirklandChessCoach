using System.Net;

namespace ChessCoach.Infrastructure.Updates;

/// <summary>
/// GET requests that follow redirects by hand, so every hop is checked. GitHub serves release files from a redirect to
/// another of its hosts; a redirect anywhere else is refused. Use with an <see cref="HttpClient"/> whose handler has
/// <c>AllowAutoRedirect = false</c>.
/// </summary>
internal static class TrustedDownload
{
    private const int MaxRedirects = 5;

    /// <summary>Only https on the default port, to github.com, api.github.com or a githubusercontent.com host.</summary>
    public static bool IsTrustedHop(Uri url) =>
        url.Scheme == Uri.UriSchemeHttps
        && url.IsDefaultPort
        && (url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || url.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase)
            || url.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase));

    public static async Task<HttpResponseMessage> GetAsync(
        HttpClient http, Uri url, HttpCompletionOption completion, CancellationToken cancellationToken, Action<HttpRequestMessage>? configure = null)
    {
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            if (!IsTrustedHop(url))
            {
                throw new InvalidOperationException($"{url.Host} is not a GitHub host, so it was not contacted.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            configure?.Invoke(request);
            var response = await http.SendAsync(request, completion, cancellationToken);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
                or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect
                && response.Headers.Location is { } location)
            {
                response.Dispose();
                url = location.IsAbsoluteUri ? location : new Uri(url, location);
                continue;
            }

            return response;
        }

        throw new InvalidOperationException("The download redirected too many times.");
    }
}
