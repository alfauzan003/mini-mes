using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;

namespace MiniMes.Api.Shared.Http;

public static class JsonSetup
{
    public static void ConfigureMesJson(this JsonOptions options) =>
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
}
