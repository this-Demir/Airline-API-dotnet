using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace AirlineSystem.API.IntegrationTests.Endpoints;

/// <summary>
/// Integration smoke tests for <c>/api/v1/airports</c> CRUD endpoints (FR-02).
/// Verifies Admin-only access and the happy-path airport creation flow.
/// </summary>
public class AirportsEndpointsTests : IntegrationTestBase
{
    public AirportsEndpointsTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Auth guards ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/airports");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_CustomerToken_Returns403()
    {
        // Arrange
        var token   = await GetCustomerTokenAsync();
        var request = Authorized(HttpMethod.Get, "/api/v1/airports", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_NoToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/airports",
            new { code = "TST", name = "Test Airport", city = "Testville" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Happy path — Admin ───────────────────────────────────────────────────

    [Fact]
    public async Task Create_AdminToken_Returns201WithLocationAndBody()
    {
        // Arrange
        var token   = await GetAdminTokenAsync();
        var request = Authorized(HttpMethod.Post, "/api/v1/airports", token);
        request.Content = JsonContent.Create(new
        {
            code = "TST",
            name = "Test Airport",
            city = "Testville"
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert — 201 Created with Location header pointing to the new resource
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("TST");
        doc.RootElement.GetProperty("city").GetString().Should().Be("Testville");
    }

    // ── Role guard — Create ───────────────────────────────────────────────────

    [Fact]
    public async Task Create_CustomerToken_Returns403()
    {
        // Arrange
        var token   = await GetCustomerTokenAsync();
        var request = Authorized(HttpMethod.Post, "/api/v1/airports", token);
        request.Content = JsonContent.Create(new { code = "CST", name = "Customer Test", city = "City" });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Duplicate code — Create ───────────────────────────────────────────────

    [Fact]
    public async Task Create_AdminToken_DuplicateCode_Returns400()
    {
        // Arrange — first creation succeeds
        var token = await GetAdminTokenAsync();
        var first = Authorized(HttpMethod.Post, "/api/v1/airports", token);
        first.Content = JsonContent.Create(new { code = "DUP", name = "Dup Airport", city = "DupCity" });
        await _client.SendAsync(first);

        // Act — second creation with the same code
        var second = Authorized(HttpMethod.Post, "/api/v1/airports", token);
        second.Content = JsonContent.Create(new { code = "DUP", name = "Dup Airport 2", city = "DupCity" });
        var response = await _client.SendAsync(second);

        // Assert — InvalidOperationException mapped to 400 by ExceptionHandlingMiddleware
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GetById ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_AdminToken_ExistingId_Returns200WithBody()
    {
        // Arrange — create an airport and capture its Id
        var token   = await GetAdminTokenAsync();
        var create  = Authorized(HttpMethod.Post, "/api/v1/airports", token);
        create.Content = JsonContent.Create(new { code = "GBI", name = "GetById Airport", city = "GbiCity" });
        var createResp = await _client.SendAsync(create);
        using var createDoc = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
        var id = createDoc.RootElement.GetProperty("id").GetString();

        // Act
        var getReq  = Authorized(HttpMethod.Get, $"/api/v1/airports/{id}", token);
        var response = await _client.SendAsync(getReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("GBI");
    }

    [Fact]
    public async Task GetById_AdminToken_UnknownId_Returns404()
    {
        // Arrange
        var token   = await GetAdminTokenAsync();
        var request = Authorized(HttpMethod.Get, $"/api/v1/airports/{Guid.NewGuid()}", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_AdminToken_ExistingId_Returns204()
    {
        // Arrange — create an airport, capture its Id
        var token  = await GetAdminTokenAsync();
        var create = Authorized(HttpMethod.Post, "/api/v1/airports", token);
        create.Content = JsonContent.Create(new { code = "UPD", name = "Update Airport", city = "UpdCity" });
        var createResp = await _client.SendAsync(create);
        using var createDoc = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
        var id = createDoc.RootElement.GetProperty("id").GetString();

        // Act
        var put  = Authorized(HttpMethod.Put, $"/api/v1/airports/{id}", token);
        put.Content = JsonContent.Create(new { code = "UPD", name = "Updated Name", city = "NewCity" });
        var response = await _client.SendAsync(put);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_AdminToken_UnknownId_Returns404()
    {
        // Arrange
        var token   = await GetAdminTokenAsync();
        var request = Authorized(HttpMethod.Put, $"/api/v1/airports/{Guid.NewGuid()}", token);
        request.Content = JsonContent.Create(new { code = "XXX", name = "No Airport", city = "Nowhere" });

        // Act
        var response = await _client.SendAsync(request);

        // Assert — KeyNotFoundException mapped to 404 by ExceptionHandlingMiddleware
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_AdminToken_ExistingId_Returns204()
    {
        // Arrange — create an airport, capture its Id
        var token  = await GetAdminTokenAsync();
        var create = Authorized(HttpMethod.Post, "/api/v1/airports", token);
        create.Content = JsonContent.Create(new { code = "DEL", name = "Delete Airport", city = "DelCity" });
        var createResp = await _client.SendAsync(create);
        using var createDoc = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
        var id = createDoc.RootElement.GetProperty("id").GetString();

        // Act
        var del      = Authorized(HttpMethod.Delete, $"/api/v1/airports/{id}", token);
        var response = await _client.SendAsync(del);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_AdminToken_UnknownId_Returns404()
    {
        // Arrange
        var token   = await GetAdminTokenAsync();
        var request = Authorized(HttpMethod.Delete, $"/api/v1/airports/{Guid.NewGuid()}", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — KeyNotFoundException mapped to 404 by ExceptionHandlingMiddleware
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Batch Insert ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBatch_AdminToken_ValidList_Returns200WithCreatedAirports()
    {
        // Arrange
        var token   = await GetAdminTokenAsync();
        var request = Authorized(HttpMethod.Post, "/api/v1/airports/batch", token);
        request.Content = JsonContent.Create(new[]
        {
            new { code = "BA1", name = "Batch Airport One", city = "CityOne" },
            new { code = "BA2", name = "Batch Airport Two", city = "CityTwo" }
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("message").GetString().Should().Contain("Successfully added 2");

        var airports = doc.RootElement.GetProperty("airports").EnumerateArray().ToList();
        airports.Should().HaveCount(2);
        airports.Select(a => a.GetProperty("code").GetString())
                .Should().BeEquivalentTo(["BA1", "BA2"]);

        // No codes were skipped — all were new
        doc.RootElement.GetProperty("skippedCodes").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task CreateBatch_AdminToken_DuplicateCode_Returns200WithSkippedCodes()
    {
        // S0: pre-create "BCH" via single endpoint so it already exists in the DB
        var token = await GetAdminTokenAsync();
        var seed  = Authorized(HttpMethod.Post, "/api/v1/airports", token);
        seed.Content = JsonContent.Create(new { code = "BCH", name = "Batch Conflict Hub", city = "ConflictCity" });
        await _client.SendAsync(seed);

        // S1: batch containing the already-existing code plus a genuinely new code
        var request = Authorized(HttpMethod.Post, "/api/v1/airports/batch", token);
        request.Content = JsonContent.Create(new[]
        {
            new { code = "BCH", name = "Batch Conflict Hub Dup", city = "ConflictCity" },
            new { code = "BCX", name = "Batch Conflict Extra",   city = "ExtraCity"    }
        });
        var response = await _client.SendAsync(request);

        // S2: insert-ignore → still 200, BCH is in skippedCodes, BCX is inserted
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("message").GetString().Should().Contain("Skipped 1 duplicate(s)");

        var airports = doc.RootElement.GetProperty("airports").EnumerateArray().ToList();
        airports.Should().HaveCount(1);
        airports[0].GetProperty("code").GetString().Should().Be("BCX");

        var skipped = doc.RootElement.GetProperty("skippedCodes")
                         .EnumerateArray()
                         .Select(e => e.GetString())
                         .ToList();
        skipped.Should().Contain("BCH");
    }
}
