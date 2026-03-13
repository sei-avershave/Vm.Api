// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using Player.Vm.Api.Tests.Integration.Fixtures;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Player.Vm.Api.Tests.Integration.Tests.Controllers;

[Category("Integration")]
[ClassDataSource<VmTestContext>(Shared = SharedType.PerTestSession)]
public class HealthCheckTests(VmTestContext factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Test]
    public async Task GetLiveliness_WhenHealthy_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/health/live");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetReadiness_WhenHealthy_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/health/ready");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
