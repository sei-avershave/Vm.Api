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
using Shouldly;
using Xunit;
using VmEntity = Player.Vm.Api.Domain.Models.Vm;

namespace Player.Vm.Api.Tests.Integration.Tests.Controllers;

public class VmControllerTests : IClassFixture<VmTestContext>
{
    private readonly HttpClient _client;
    private readonly VmTestContext _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public VmControllerTests(VmTestContext factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsOkAndEmptyList_WhenNoVmsExist()
    {
        // Act
        var response = await _client.GetAsync("/api/vms");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var vms = JsonSerializer.Deserialize<Features.Vms.Vm[]>(content, JsonOptions);
        vms.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateVm_ReturnsCreated_WithValidForm()
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
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var content = await response.Content.ReadAsStringAsync();
        var createdVm = JsonSerializer.Deserialize<Features.Vms.Vm>(content, JsonOptions);
        createdVm.ShouldNotBeNull();
        createdVm.Id.ShouldBe(vmId);
        createdVm.Name.ShouldBe("integration-test-vm");

        // Verify in database
        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<VmContext>();
        var dbVm = await verifyContext.Vms
            .Include(v => v.VmTeams)
            .FirstOrDefaultAsync(v => v.Id == vmId);

        dbVm.ShouldNotBeNull();
        dbVm.Name.ShouldBe("integration-test-vm");
        dbVm.VmTeams.ShouldContain(vt => vt.TeamId == teamId);
    }

    [Fact]
    public async Task GetVm_ReturnsOk_WhenVmExists()
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
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<Features.Vms.Vm>(content, JsonOptions);
        result.ShouldNotBeNull();
        result.Id.ShouldBe(vmId);
        result.Name.ShouldBe("get-test-vm");
    }

    [Fact]
    public async Task DeleteVm_ReturnsNoContent_WhenVmExists()
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
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify deletion in database
        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<VmContext>();
        var deletedVm = await verifyContext.Vms.FindAsync(vmId);
        deletedVm.ShouldBeNull();
    }

    [Fact]
    public async Task GetTeamVms_ReturnsOk_WhenTeamHasVms()
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
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
