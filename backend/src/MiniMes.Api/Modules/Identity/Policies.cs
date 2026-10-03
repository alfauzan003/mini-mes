using Microsoft.AspNetCore.Authorization;
using MiniMes.Api.Modules.Equipment.Machine;

namespace MiniMes.Api.Modules.Identity;

public static class Policies
{
    public const string Plan = "plan";
    public const string Operate = "operate";
    public const string Inspect = "inspect";
    public const string Admin = "admin";
    public const string Machine = "machine";

    public static void AddMesPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(Plan, p => p.RequireRole(nameof(Role.Planner), nameof(Role.Admin)));
        options.AddPolicy(Operate, p => p.RequireRole(nameof(Role.Operator), nameof(Role.Admin)));
        options.AddPolicy(Inspect, p => p.RequireRole(nameof(Role.QC), nameof(Role.Admin)));
        options.AddPolicy(Admin, p => p.RequireRole(nameof(Role.Admin)));
        options.AddPolicy(Machine, p => p
            .AddAuthenticationSchemes(MachineKeyAuthenticationHandler.SchemeName)
            .RequireRole(MachineKeyAuthenticationHandler.RoleName));
    }
}
