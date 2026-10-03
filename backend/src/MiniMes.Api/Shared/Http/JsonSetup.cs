using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;

namespace MiniMes.Api.Shared.Http;

public static class JsonSetup
{
    public static void ConfigureMesJson(this JsonOptions options) => options.SerializerOptions.AddMesConverters();

    /// <summary>The converters shared by HTTP responses and SignalR payloads, so enums read the same in both.</summary>
    public static void AddMesConverters(this JsonSerializerOptions options) =>
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
}
