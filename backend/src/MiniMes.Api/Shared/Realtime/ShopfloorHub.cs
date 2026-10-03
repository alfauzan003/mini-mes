using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MiniMes.Api.Shared.Realtime;

/// <summary>Pushes equipment, lot, work order and alarm changes to signed-in shop floor screens. Clients only listen.</summary>
[Authorize]
public sealed class ShopfloorHub : Hub;
