using Healthcare.Application.Ports.Authentication;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace Healthcare.Adapters.Authentication;

public sealed class HaveIBeenPwnedPasswordChecker : IBreachedPasswordChecker
{
    private readonly HttpClient _httpClient;
    private readonly bool _enabled;

    public HaveIBeenPwnedPasswordChecker(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://api.pwnedpasswords.com/");
        _enabled = configuration.GetValue("Authentication:BreachedPasswordCheckEnabled", true);
    }

    public async Task<bool> IsPasswordBreachedAsync(string password, CancellationToken cancellationToken = default)
    {
        if (!_enabled)
            return false;

        var sha1Hash = SHA1.HashData(Encoding.UTF8.GetBytes(password));
        var hexHash = Convert.ToHexString(sha1Hash).ToLowerInvariant();
        var prefix = hexHash[..5];
        var suffix = hexHash[5..];

        using var response = await _httpClient.GetAsync($"range/{prefix}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var parts = line.Trim().Split(':');
            if (parts.Length == 2 && parts[0].Equals(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
