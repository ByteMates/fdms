using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace ClaimService.Infrastructure.Handlers;

public sealed class AuthForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _ctx;
    public AuthForwardingHandler(IHttpContextAccessor ctx) => _ctx = ctx;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _ctx.HttpContext?.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(token);
        return base.SendAsync(request, cancellationToken);
    }
}
