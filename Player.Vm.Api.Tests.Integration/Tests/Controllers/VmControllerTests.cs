// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Crucible.Common.Testing.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Player.Vm.Api.Data;
using Player.Vm.Api.Domain.Models;
using Player.Vm.Api.Features.Vms;
using Player.Vm.Api.Tests.Integration.Fixtures;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using VmEntity = Player.Vm.Api.Domain.Models.Vm;

namespace Player.Vm.Api.Tests.Integration.Tests.Controllers;

[Category("Integration")]
[ClassDataSource<VmTestContext>(Shared = SharedType.PerTestSession)]
public class VmControllerTests(VmTestContext factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly VmTestContext _factory = factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Test]
    public async Task GetAll_WhenNoVmsExist_ReturnsOkAndEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/api/vms");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var vms = JsonSerializer.Deserialize<Features.Vms.Vm[]>(content, JsonOptions);
        await Assert.That(vms).IsNotNull();
    }

    [Test]
    public async Task CreateVm_WhenFormIsValid_ReturnsCreated()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var vmId = Guid.NewGuid();

        // Seed a VmTeam record context so the service can find the team
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<VmContext>();
            // Ensure clean state for this test's Vm ID
            var existingVm = await dbContext.Vms.FindAsync(vmId);
            if (existingVm != null)
            {
                dbContext.Vms.Remove(existingVm);
                await dbContext.SaveChangesAsync();
            }
        }

        var form = new VmCreateForm
        {
            Id = vmId,
            Name = "integration-test-vm",
            TeamIds = new List<Guid> { teamId },
            Embeddable = true
        };

        // Act
        var response = await _client.PostAsync("/api/vms", form.ToJsonBody());

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var content = await response.Content.ReadAsStringAsync();
        var createdVm = JsonSerializer.Deserialize<Features.Vms.Vm>(content, JsonOptions);
        await Assert.That(createdVm).IsNotNull();
        await Assert.That(createdVm!.Id).IsEqualTo(vmId);
        await Assert.That(createdVm.Name).IsEqualTo("integration-test-vm");

        // Verify in database
        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<VmContext>();
        var dbVm = await verifyContext.Vms
            .Include(v => v.VmTeams)
            .FirstOrDefaultAsync(v => v.Id == vmId);

        await Assert.That(dbVm).IsNotNull();
        await Assert.That(dbVm!.Name).IsEqualTo("integration-test-vm");
        await Assert.That(dbVm.VmTeams.Any(vt => vt.TeamId == teamId)).IsTrue();
    }

    [Test]
    public async Task GetVm_WhenVmExists_ReturnsOk()
    {
        // Arrange - create a VM first
        var teamId = Guid.NewGuid();
        var vmId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<VmContext>();
            var vm = new VmEntity
            {
                Id = vmId,
                Name = "get-test-vm",
                PowerState = PowerState.On,
                Type = VmType.Vsphere,
                Embeddable = true,
                AllowedNetworks = Array.Empty<string>(),
                IpAddresses = Array.Empty<string>(),
                VmTeams = new List<VmTeam> { new(teamId, vmId) }
            };
            dbContext.Vms.Add(vm);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await _client.GetAsync($"/api/vms/{vmId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<Features.Vms.Vm>(content, JsonOptions);
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(vmId);
        await Assert.That(result.Name).IsEqualTo("get-test-vm");
    }

    [Test]
    public async Task DeleteVm_WhenVmExists_ReturnsNoContent()
    {
        // Arrange - create a VM first
        var teamId = Guid.NewGuid();
        var vmId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<VmContext>();
            var vm = new VmEntity
            {
                Id = vmId,
                Name = "delete-test-vm",
                PowerState = PowerState.Off,
                Type = VmType.Vsphere,
                Embeddable = true,
                AllowedNetworks = Array.Empty<string>(),
                IpAddresses = Array.Empty<string>(),
                VmTeams = new List<VmTeam> { new(teamId, vmId) }
            };
            dbContext.Vms.Add(vm);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await _client.DeleteAsync($"/api/vms/{vmId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Verify deletion in database
        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<VmContext>();
        var deletedVm = await verifyContext.Vms.FindAsync(vmId);
        await Assert.That(deletedVm).IsNull();
    }

    [Test]
    public async Task GetTeamVms_WhenTeamHasVms_ReturnsOk()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var vmId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<VmContext>();
            var vm = new VmEntity
            {
                Id = vmId,
                Name = "team-vm",
                PowerState = PowerState.On,
                Type = VmType.Vsphere,
                Embeddable = true,
                AllowedNetworks = Array.Empty<string>(),
                IpAddresses = Array.Empty<string>(),
                VmTeams = new List<VmTeam> { new(teamId, vmId) }
            };
            dbContext.Vms.Add(vm);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await _client.GetAsync($"/api/teams/{teamId}/vms");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
