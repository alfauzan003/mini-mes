using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using MiniMes.Api.Modules.Equipment.Machine;
using Microsoft.IdentityModel.Tokens;
using MiniMes.Api.Modules.Identity.Features.DemoLogin;
using MiniMes.Api.Modules.Identity.Features.Login;
using MiniMes.Api.Modules.Identity.Features.Me;

namespace MiniMes.Api.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<JwtTokenService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddOptions<MachineOptions>()
            .BindConfiguration(MachineOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer()
            .AddScheme<AuthenticationSchemeOptions, MachineKeyAuthenticationHandler>(
                MachineKeyAuthenticationHandler.SchemeName, null);

        // Resolved lazily so test hosts can override Jwt settings after Program starts.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = JwtTokenService.SigningKey(jwt.Value.Key),
                    NameClaimType = "name",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
                bearer.Events = new JwtBearerEvents
                {
                    // Browsers cannot set headers on WebSocket and server-sent event requests, so SignalR sends
                    // the token in the query string. Only hub paths accept it there.
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"].ToString();
                        if (token.Length > 0 && context.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddMesPolicies();
            // Every endpoint requires a signed-in user unless it opts out with AllowAnonymous.
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app, bool enableDemoLogin)
    {
        app.MapLogin();
        app.MapMe();
        if (enableDemoLogin)
        {
            app.MapDemoLogin();
        }

        return app;
    }
}
