using System.Net.Http.Json;
using ClaimService.Application.Interfaces;
using System.Net;
using System.Net.Http.Json;
using ClaimService.Infrastructure.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace ClaimService.Infrastructure.Clients;

public sealed class EmployeeServiceClient : IEmployeeServiceClient
{
    private readonly HttpClient _http;
    private readonly IDistributedCache _cache;
    private readonly DistributedCacheEntryOptions _cacheOptions;

    public EmployeeServiceClient(HttpClient http, IDistributedCache cache, IOptions<CachingOptions> opts)
    {
        _http = http;
        _cache = cache;

        var ttl = TimeSpan.FromMinutes(Math.Max(1, opts.Value.EmployeeLookupMinutes));
        _cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        };
    }

    private sealed record ResolveResponse(string EmployeeId);

    public async Task<string?> ResolveEmployeeIdAsync(string? cnic, string? personnelNo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cnic) && string.IsNullOrWhiteSpace(personnelNo))
            return null;

        // Prefer CNIC key if both provided (you can flip this if PersonnelNo is the primary index)
        var cacheKey = BuildCacheKey(cnic, personnelNo);

        // 1) Try cache
        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrWhiteSpace(cached))
            return cached;

        // 2) Call EmployeeService
        // Adjust the path to your real API route; this example assumes:
        // GET employees/lookup?cnic=...&personnelNo=...
        var path = BuildLookupPath(cnic, personnelNo);

        try
        {
            using var resp = await _http.GetAsync(path, ct);
            if (!resp.IsSuccessStatusCode)
                return null;

            var dto = await resp.Content.ReadFromJsonAsync<EmployeeLookupDto>(cancellationToken: ct);
            var employeeId = dto?.EmployeeId;

            if (!string.IsNullOrWhiteSpace(employeeId))
                await _cache.SetStringAsync(cacheKey, employeeId, _cacheOptions, ct);

            return employeeId;
        }
        catch
        {
            // Don’t let lookup failures break calling flows
            return null;
        }
    }

    private static string BuildCacheKey(string? cnic, string? personnelNo)
    {
        if (!string.IsNullOrWhiteSpace(cnic))
            return $"emp:cnic:{cnic.Trim()}";
        return $"emp:pn:{personnelNo!.Trim()}";
    }

    private static string BuildLookupPath(string? cnic, string? personnelNo)
    {
        var hasCnic = !string.IsNullOrWhiteSpace(cnic);
        var hasPn = !string.IsNullOrWhiteSpace(personnelNo);

        if (hasCnic && hasPn)
            return $"employees/lookup?cnic={WebUtility.UrlEncode(cnic)}&personnelNo={WebUtility.UrlEncode(personnelNo)}";
        if (hasCnic)
            return $"employees/lookup?cnic={WebUtility.UrlEncode(cnic)}";
        return $"employees/lookup?personnelNo={WebUtility.UrlEncode(personnelNo!)}";
    }

    // Adjust to match your EmployeeService response
    private sealed class EmployeeLookupDto
    {
        public string? EmployeeId { get; set; }
    }
}
