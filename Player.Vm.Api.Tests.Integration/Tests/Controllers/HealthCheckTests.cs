// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using Player.Vm.Api.Tests.Integration.Fixtures;
using Shouldly;
using Xunit;

namespace Player.Vm.Api.Tests.Integration.Tests.Controllers;

public class HealthCheckTests : IClassFixture<VmTestContext>
{
    private readonly HttpClient _client;

    public HealthCheckTests(VmTestContext factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetLiveliness_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/health/live");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetReadiness_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/health/ready");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
