using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Processes.Tests;

public class HealthEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", content);
    }

    [Fact]
    public async Task Root_ReturnsWelcome()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Welcome", content);
    }

    [Fact]
    public async Task Ready_ReturnsServiceUnavailableOrOk()
    {
        var response = await _client.GetAsync("/ready");
        // Could be 200 or 503 depending on StartupRecoveryService
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable });
    }

    [Fact]
    public async Task GetProcesses_ReturnsOk()
    {
        var response = await _client.GetAsync("/processes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostProcesses_InvalidBody_ReturnsBadRequest()
    {
        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/processes", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetProcessById_InvalidId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/processes/not-a-valid-id");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetSubprocesses_ReturnsOk()
    {
        var response = await _client.GetAsync("/subprocesses");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSubprocessById_InvalidId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/subprocesses/not-a-valid-id");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
