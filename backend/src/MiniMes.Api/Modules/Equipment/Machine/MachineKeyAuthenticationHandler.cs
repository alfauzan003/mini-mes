using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MiniMes.Api.Modules.Equipment.Machine;

/// <summary>Authenticates equipment simulators by the <c>X-Machine-Key</c> header and gives them the Machine role.</summary>
public sealed class MachineKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<MachineOptions> machine) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "MachineKey";
    public const string HeaderName = "X-Machine-Key";
    public const string RoleName = "Machine";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values) || values.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var presented = Encoding.UTF8.GetBytes(values[0] ?? "");
        var expected = Encoding.UTF8.GetBytes(machine.Value.ApiKey);
        if (!CryptographicOperations.FixedTimeEquals(presented, expected))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid machine key."));
        }

        var identity = new ClaimsIdentity(
            [new Claim("name", "machine"), new Claim("role", RoleName)], SchemeName, "name", "role");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
