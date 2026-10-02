using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MiniMes.IntegrationTests.WorkOrders;

[Collection("api")]
public class WorkOrderEndpointTests(MesApiFactory api) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await api.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string errorCode)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(errorCode, (await ReadAsync(response)).GetProperty("errorCode").GetString());
    }

    private static object CreateRequest(
        string product = "CATH-NCM811", int target = 8, string mix = "MX01", string coat = "CT01",
        string cal = "CP01", string slit = "SL01") => new
        {
            productCode = product,
            targetQty = target,
            plannedStart = "2026-10-05T01:00:00Z",
            plannedEnd = "2026-10-06T01:00:00Z",
            operations = new[]
            {
                new { operation = "MIX", equipmentCode = mix },
                new { operation = "COAT", equipmentCode = coat },
                new { operation = "CAL", equipmentCode = cal },
                new { operation = "SLIT", equipmentCode = slit }
            }
        };

    private static async Task<JsonElement> CreateAsync(HttpClient planner, object? request = null)
    {
        var response = await planner.PostAsJsonAsync("/api/work-orders", request ?? CreateRequest(), Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync(response);
    }

    private static async Task<JsonElement> PostActionAsync(HttpClient client, JsonElement wo, string action)
    {
        var response = await client.PostAsync($"/api/work-orders/{wo.GetProperty("id").GetGuid()}/{action}", null, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    [Fact]
    public async Task Planner_creates_planned_work_order_with_generated_number()
    {
        var planner = await api.ClientAsAsync("planner");

        var response = await planner.PostAsJsonAsync("/api/work-orders", CreateRequest(), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var wo = await ReadAsync(response);
        Assert.Matches(@"^WO-\d{6}-001$", wo.GetProperty("number").GetString());
        Assert.Equal($"/api/work-orders/{wo.GetProperty("id").GetGuid()}", response.Headers.Location?.OriginalString);
        Assert.Equal("PLANNED", wo.GetProperty("status").GetString());
        Assert.Equal("CATH-NCM811", wo.GetProperty("productCode").GetString());
        Assert.Equal("CATHODE", wo.GetProperty("polarity").GetString());
        Assert.Equal(8, wo.GetProperty("targetQty").GetInt32());
        Assert.Equal(0, wo.GetProperty("goodCount").GetInt32());
        Assert.Equal(4, wo.GetProperty("operations").GetArrayLength());
    }

    [Fact]
    public async Task Created_operations_persist_in_route_order_with_equipment()
    {
        var planner = await api.ClientAsAsync("planner");
        var created = await CreateAsync(planner, CreateRequest(coat: " ct02 "));

        var fetched = await planner.GetFromJsonAsync<JsonElement>(
            $"/api/work-orders/{created.GetProperty("id").GetGuid()}", Ct);

        var operations = fetched.GetProperty("operations").EnumerateArray().ToList();
        Assert.Equal(["MIX", "COAT", "CAL", "SLIT"], operations.Select(o => o.GetProperty("operation").GetString()));
        Assert.Equal(["MX01", "CT02", "CP01", "SL01"], operations.Select(o => o.GetProperty("equipmentCode").GetString()));
        var seqs = operations.Select(o => o.GetProperty("seq").GetInt32()).ToList();
        Assert.Equal(seqs.Order(), seqs);
        Assert.Equal(4, seqs.Distinct().Count());
    }

    [Fact]
    public async Task Work_order_numbers_increment_within_the_day()
    {
        var planner = await api.ClientAsAsync("planner");

        var first = await CreateAsync(planner);
        var second = await CreateAsync(planner);

        Assert.EndsWith("-001", first.GetProperty("number").GetString());
        Assert.EndsWith("-002", second.GetProperty("number").GetString());
    }

    [Fact]
    public async Task Operator_cannot_create_work_order()
    {
        var op = await api.ClientAsAsync("operator");

        var response = await op.PostAsJsonAsync("/api/work-orders", CreateRequest(), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_product_is_404_product_not_found()
    {
        var planner = await api.ClientAsAsync("planner");

        var response = await planner.PostAsJsonAsync("/api/work-orders", CreateRequest(product: "NOPE"), Ct);

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task Unknown_equipment_is_404_equipment_not_found_and_writes_nothing()
    {
        var planner = await api.ClientAsAsync("planner");

        var response = await planner.PostAsJsonAsync("/api/work-orders", CreateRequest(mix: "MX99"), Ct);

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "EQUIPMENT_NOT_FOUND");
        var all = await planner.GetFromJsonAsync<JsonElement>("/api/work-orders", Ct);
        Assert.Equal(0, all.GetArrayLength());
    }

    [Fact]
    public async Task Coater_on_mix_step_is_422_invalid_assignment()
    {
        var planner = await api.ClientAsAsync("planner");

        var response = await planner.PostAsJsonAsync("/api/work-orders", CreateRequest(mix: "CT01"), Ct);

        await AssertErrorAsync(response, HttpStatusCode.UnprocessableEntity, "WO_INVALID_ASSIGNMENT");
    }

    [Fact]
    public async Task Unknown_operation_code_is_a_400_bad_request()
    {
        var planner = await api.ClientAsAsync("planner");
        var body = new
        {
            productCode = "CATH-NCM811",
            targetQty = 8,
            plannedStart = "2026-10-05T01:00:00Z",
            plannedEnd = "2026-10-06T01:00:00Z",
            operations = new[] { new { operation = "WELD", equipmentCode = "MX01" } }
        };

        var response = await planner.PostAsJsonAsync("/api/work-orders", body, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Null_operation_element_is_a_400_bad_request_on_create_and_update()
    {
        var planner = await api.ClientAsAsync("planner");
        var created = await CreateAsync(planner);
        var body = new
        {
            productCode = "CATH-NCM811",
            targetQty = 8,
            plannedStart = "2026-10-05T01:00:00Z",
            plannedEnd = "2026-10-06T01:00:00Z",
            operations = new object?[] { null }
        };

        var create = await planner.PostAsJsonAsync("/api/work-orders", body, Ct);
        var update = await planner.PutAsJsonAsync($"/api/work-orders/{created.GetProperty("id").GetGuid()}", body, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task Update_changes_target_dates_and_equipment()
    {
        var planner = await api.ClientAsAsync("planner");
        var created = await CreateAsync(planner);
        var update = new
        {
            targetQty = 12,
            plannedStart = "2026-10-07T01:00:00Z",
            plannedEnd = "2026-10-08T01:00:00Z",
            operations = new[]
            {
                new { operation = "MIX", equipmentCode = "MX02" },
                new { operation = "COAT", equipmentCode = "CT01" },
                new { operation = "CAL", equipmentCode = "CP02" },
                new { operation = "SLIT", equipmentCode = "SL01" }
            }
        };

        var response = await planner.PutAsJsonAsync($"/api/work-orders/{created.GetProperty("id").GetGuid()}", update, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var wo = await ReadAsync(response);
        Assert.Equal(12, wo.GetProperty("targetQty").GetInt32());
        Assert.Equal(DateTimeOffset.Parse("2026-10-07T01:00:00Z"), wo.GetProperty("plannedStart").GetDateTimeOffset());
        Assert.Equal(
            ["MX02", "CT01", "CP02", "SL01"],
            wo.GetProperty("operations").EnumerateArray().Select(o => o.GetProperty("equipmentCode").GetString()));
        Assert.Equal(created.GetProperty("number").GetString(), wo.GetProperty("number").GetString());
    }

    [Fact]
    public async Task Update_after_release_is_422_not_editable()
    {
        var planner = await api.ClientAsAsync("planner");
        var created = await CreateAsync(planner);
        await PostActionAsync(planner, created, "release");
        var update = new
        {
            targetQty = 12,
            plannedStart = "2026-10-07T01:00:00Z",
            plannedEnd = "2026-10-08T01:00:00Z",
            operations = new[]
            {
                new { operation = "MIX", equipmentCode = "MX01" },
                new { operation = "COAT", equipmentCode = "CT01" },
                new { operation = "CAL", equipmentCode = "CP01" },
                new { operation = "SLIT", equipmentCode = "SL01" }
            }
        };

        var response = await planner.PutAsJsonAsync($"/api/work-orders/{created.GetProperty("id").GetGuid()}", update, Ct);

        await AssertErrorAsync(response, HttpStatusCode.UnprocessableEntity, "WO_NOT_EDITABLE");
    }

    [Fact]
    public async Task Release_hold_resume_round_trip()
    {
        var planner = await api.ClientAsAsync("planner");
        var created = await CreateAsync(planner);

        var released = await PostActionAsync(planner, created, "release");
        Assert.Equal("RELEASED", released.GetProperty("status").GetString());
        var held = await PostActionAsync(planner, created, "hold");
        Assert.Equal("HOLD", held.GetProperty("status").GetString());
        var resumed = await PostActionAsync(planner, created, "resume");

        Assert.Equal("RELEASED", resumed.GetProperty("status").GetString());
        var fetched = await planner.GetFromJsonAsync<JsonElement>(
            $"/api/work-orders/{created.GetProperty("id").GetGuid()}", Ct);
        Assert.Equal("RELEASED", fetched.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Complete_planned_order_is_422_invalid_transition()
    {
        var planner = await api.ClientAsAsync("planner");
        var created = await CreateAsync(planner);

        var response = await planner.PostAsync($"/api/work-orders/{created.GetProperty("id").GetGuid()}/complete", null, Ct);

        await AssertErrorAsync(response, HttpStatusCode.UnprocessableEntity, "WO_INVALID_TRANSITION");
    }

    [Fact]
    public async Task Operator_cannot_release_work_order()
    {
        var planner = await api.ClientAsAsync("planner");
        var op = await api.ClientAsAsync("operator");
        var created = await CreateAsync(planner);

        var response = await op.PostAsync($"/api/work-orders/{created.GetProperty("id").GetGuid()}/release", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_work_order_is_404_wo_not_found()
    {
        var planner = await api.ClientAsAsync("planner");
        var id = Guid.NewGuid();

        await AssertErrorAsync(await planner.GetAsync($"/api/work-orders/{id}", Ct), HttpStatusCode.NotFound, "WO_NOT_FOUND");
        await AssertErrorAsync(
            await planner.PostAsync($"/api/work-orders/{id}/release", null, Ct), HttpStatusCode.NotFound, "WO_NOT_FOUND");
    }

    [Fact]
    public async Task List_filters_by_status_and_is_newest_first()
    {
        var planner = await api.ClientAsAsync("planner");
        var first = await CreateAsync(planner);
        var second = await CreateAsync(planner);
        await PostActionAsync(planner, first, "release");

        var all = await planner.GetFromJsonAsync<JsonElement>("/api/work-orders", Ct);
        var released = await planner.GetFromJsonAsync<JsonElement>("/api/work-orders?status=RELEASED", Ct);
        var planned = await planner.GetFromJsonAsync<JsonElement>("/api/work-orders?status=planned", Ct);
        var bad = await planner.GetAsync("/api/work-orders?status=NOPE", Ct);

        Assert.Equal(
            [second.GetProperty("number").GetString(), first.GetProperty("number").GetString()],
            all.EnumerateArray().Select(w => w.GetProperty("number").GetString()));
        Assert.Equal(
            [first.GetProperty("number").GetString()],
            released.EnumerateArray().Select(w => w.GetProperty("number").GetString()));
        Assert.Equal(
            [second.GetProperty("number").GetString()],
            planned.EnumerateArray().Select(w => w.GetProperty("number").GetString()));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Assignments_show_released_orders_only()
    {
        var planner = await api.ClientAsAsync("planner");
        await CreateAsync(planner);
        var releasedOrder = await CreateAsync(planner);
        await PostActionAsync(planner, releasedOrder, "release");

        var assignments = await planner.GetFromJsonAsync<JsonElement>("/api/equipment/CT01/assignments", Ct);

        var only = Assert.Single(assignments.EnumerateArray());
        Assert.Equal(releasedOrder.GetProperty("number").GetString(), only.GetProperty("workOrderNumber").GetString());
        Assert.Equal("CATH-NCM811", only.GetProperty("productCode").GetString());
        Assert.Equal("RELEASED", only.GetProperty("status").GetString());
        Assert.Equal(8, only.GetProperty("targetQty").GetInt32());
        Assert.Equal(0, only.GetProperty("goodCount").GetInt32());
        var operationId = releasedOrder.GetProperty("operations").EnumerateArray()
            .Single(o => o.GetProperty("operation").GetString() == "COAT").GetProperty("id").GetGuid();
        Assert.Equal(operationId, only.GetProperty("workOrderOperationId").GetGuid());

        var other = await planner.GetFromJsonAsync<JsonElement>("/api/equipment/CT02/assignments", Ct);
        Assert.Equal(0, other.GetArrayLength());
    }

    [Fact]
    public async Task Assignments_include_held_orders_and_exclude_nothing_active()
    {
        var planner = await api.ClientAsAsync("planner");
        var created = await CreateAsync(planner);
        await PostActionAsync(planner, created, "release");
        await PostActionAsync(planner, created, "hold");

        var assignments = await planner.GetFromJsonAsync<JsonElement>("/api/equipment/mx01/assignments", Ct);

        var only = Assert.Single(assignments.EnumerateArray());
        Assert.Equal("HOLD", only.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Assignments_for_unknown_equipment_is_404()
    {
        var planner = await api.ClientAsAsync("planner");

        var response = await planner.GetAsync("/api/equipment/XX99/assignments", Ct);

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "EQUIPMENT_NOT_FOUND");
    }
}
