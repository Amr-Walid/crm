using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using UniGroup.CRM.Application.Features.Auth.Common;
using UniGroup.CRM.Client.Models;

namespace UniGroup.CRM.Client.Services;

/// <summary>
/// Custom <see cref="AuthenticationStateProvider"/> backed by a JWT persisted in
/// browser localStorage. Provides persistent session restore on app start,
/// token-expiry validation, and role claims for <c>AuthorizeView</c>/<c>[Authorize]</c>.
/// </summary>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private const string StorageKey = "unigroup.session";
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    private readonly LocalStorageService _storage;
    private StoredSession? _session;

    /// <summary>Raised when the session changes (login/logout).</summary>
    public event Action? SessionChanged;

    /// <summary>Initializes a new instance of the <see cref="JwtAuthenticationStateProvider"/> class.</summary>
    public JwtAuthenticationStateProvider(LocalStorageService storage) => _storage = storage;

    /// <summary>The current session, or null when signed out.</summary>
    public StoredSession? Session => _session is { IsExpired: false } ? _session : null;

    /// <inheritdoc />
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        _session ??= await _storage.GetItemAsync<StoredSession>(StorageKey);

        if (_session is null || _session.IsExpired)
        {
            if (_session is not null)
            {
                // Expired persisted session — clean it up.
                _session = null;
                await _storage.RemoveItemAsync(StorageKey);
            }

            return new AuthenticationState(Anonymous);
        }

        return new AuthenticationState(BuildPrincipal(_session));
    }

    /// <summary>
    /// Persists a successful login/registration response and notifies Blazor auth.
    /// </summary>
    public async Task SignInAsync(AuthResponse auth)
    {
        var claims = DecodeJwtPayload(auth.Token);

        _session = new StoredSession
        {
            Token = auth.Token,
            TokenExpiration = auth.TokenExpiration,
            RefreshToken = auth.RefreshToken,
            Email = auth.Email,
            FirstName = auth.FirstName,
            LastName = auth.LastName,
            UserId = claims.UserId,
            Roles = claims.Roles,
        };

        await _storage.SetItemAsync(StorageKey, _session);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(BuildPrincipal(_session))));
        SessionChanged?.Invoke();
    }

    /// <summary>Clears the persisted session and notifies Blazor auth.</summary>
    public async Task SignOutAsync()
    {
        _session = null;
        await _storage.RemoveItemAsync(StorageKey);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
        SessionChanged?.Invoke();
    }

    private static ClaimsPrincipal BuildPrincipal(StoredSession session)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.UserId),
            new(ClaimTypes.Name, session.FullName),
            new(ClaimTypes.Email, session.Email),
        };
        claims.AddRange(session.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "jwt"));
    }

    private const string RoleClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

    private static (string UserId, List<string> Roles) DecodeJwtPayload(string token)
    {
        try
        {
            var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            var root = doc.RootElement;

            var sub = root.TryGetProperty("sub", out var s) ? s.GetString() ?? "" : "";

            var roles = new List<string>();
            foreach (var key in new[] { RoleClaim, "role", "roles" })
            {
                if (!root.TryGetProperty(key, out var r))
                {
                    continue;
                }

                if (r.ValueKind == JsonValueKind.Array)
                {
                    roles.AddRange(r.EnumerateArray().Select(x => x.GetString()!).Where(x => x is not null));
                }
                else if (r.ValueKind == JsonValueKind.String)
                {
                    roles.Add(r.GetString()!);
                }
            }

            return (sub, roles.Distinct().ToList());
        }
        catch
        {
            return (string.Empty, []);
        }
    }
}
