using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace ClaimService.Infrastructure.Helpers;

public class AuthForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _http;
    public AuthForwardingHandler(IHttpContextAccessor http) => _http = http;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var auth = _http.HttpContext?.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrWhiteSpace(auth))
        {
            // e.g., "Bearer eyJ..."
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(auth);
        }
        return base.SendAsync(request, ct);
    }
}
