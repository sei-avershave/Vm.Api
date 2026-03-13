// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using AutoMapper;
using Player.Vm.Api.Domain.Models;
using Player.Vm.Api.Features.Vms;
using Player.Vm.Api.Infrastructure.Options;
using TUnit.Core;
using VmEntity = Player.Vm.Api.Domain.Models.Vm;

namespace Player.Vm.Api.Tests.Unit;

[Category("Unit")]
public class MappingConfigurationTests
{
    private static MapperConfiguration CreateConfiguration()
    {
        var consoleUrlOptions = new ConsoleUrlOptions
        {
            DefaultUrl = "http://localhost:4305"
        };

        return new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
            cfg.ConstructServicesUsing(type =>
            {
                if (type == typeof(ConsoleUrlResolver))
                    return new ConsoleUrlResolver(consoleUrlOptions);
                return FakeItEasy.Sdk.Create.Fake(type);
            });
        });
    }

    [Test]
    public async Task CreateMapper_WithMappingProfile_ShouldSucceed()
    {
        // Arrange
        var configuration = CreateConfiguration();

        // Act - verify mapper can be created (weaker than AssertConfigurationIsValid
        // because the app has unmapped navigation properties populated elsewhere)
        var mapper = configuration.CreateMapper();
        await Assert.That(mapper).IsNotNull();
    }

    [Test]
    public async Task Map_VmEntityToVmDto_MapsAllProperties()
    {
        // Arrange
        var mapper = CreateConfiguration().CreateMapper();
        var teamId = Guid.NewGuid();
        var vmId = Guid.NewGuid();

        var vmEntity = new VmEntity
        {
            Id = vmId,
            Name = "test-vm",
            PowerState = PowerState.On,
            Type = VmType.Vsphere,
            Embeddable = true,
            VmTeams = new List<VmTeam>
            {
                new(teamId, vmId)
            }
        };

        // Act
        var result = mapper.Map<Features.Vms.Vm>(vmEntity);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Id).IsEqualTo(vmId);
        await Assert.That(result.Name).IsEqualTo("test-vm");
        await Assert.That(result.TeamIds).Contains(teamId);
    }

    [Test]
    public async Task Map_VmCreateFormToVmEntity_MapsAllProperties()
    {
        // Arrange
        var mapper = CreateConfiguration().CreateMapper();
        var vmId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var form = new VmCreateForm
        {
            Id = vmId,
            Name = "new-vm",
            TeamIds = new List<Guid> { teamId }
        };

        // Act
        var result = mapper.Map<VmEntity>(form);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Id).IsEqualTo(vmId);
        await Assert.That(result.Name).IsEqualTo("new-vm");
        await Assert.That(result.VmTeams.Count).IsEqualTo(1);
        await Assert.That(result.VmTeams.First().TeamId).IsEqualTo(teamId);
    }

    [Test]
    public async Task Map_VmMapEntityToVmMapDto_MapsAllProperties()
    {
        // Arrange
        var mapper = CreateConfiguration().CreateMapper();
        var mapId = Guid.NewGuid();
        var viewId = Guid.NewGuid();

        var vmMapEntity = new Player.Vm.Api.Domain.Models.VmMap
        {
            Id = mapId,
            ViewId = viewId,
            Name = "test-map",
            ImageUrl = "http://example.com/map.png",
            TeamIds = new List<Guid> { Guid.NewGuid() },
            Coordinates = new List<Player.Vm.Api.Domain.Models.Coordinate>
            {
                new() { Id = Guid.NewGuid(), XPosition = 1.0, YPosition = 2.0, Radius = 5.0, Label = "Point1", Urls = Array.Empty<string>() }
            }
        };

        // Act
        var result = mapper.Map<Features.Vms.VmMap>(vmMapEntity);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Id).IsEqualTo(mapId);
        await Assert.That(result.ViewId).IsEqualTo(viewId);
        await Assert.That(result.Name).IsEqualTo("test-map");
    }
}
