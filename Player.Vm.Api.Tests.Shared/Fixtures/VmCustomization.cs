// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using AutoFixture;
using Player.Vm.Api.Domain.Models;
using VmEntity = Player.Vm.Api.Domain.Models.Vm;

namespace Player.Vm.Api.Tests.Shared.Fixtures;

/// <summary>
/// AutoFixture customization that registers factories for all Player VM entity types,
/// avoiding circular reference issues from EF navigation properties.
/// Handles entities for both VmContext and VmLoggingContext.
/// </summary>
public class VmCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        // Prevent infinite recursion from navigation properties
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        // --- VmContext entities ---

        fixture.Customize<VmEntity>(c => c
            .Without(x => x.VmTeams)
            .Without(x => x.ProxmoxVmInfo)
            .Without(x => x.ConsoleConnectionInfo)
            .With(x => x.Id, () => Guid.NewGuid())
            .With(x => x.PowerState, () => PowerState.On)
            .With(x => x.Type, () => VmType.Vsphere)
            .With(x => x.Embeddable, () => true)
            .With(x => x.HasSnapshot, () => false)
            .With(x => x.AllowedNetworks, () => Array.Empty<string>())
            .With(x => x.IpAddresses, () => Array.Empty<string>()));

        fixture.Customize<VmTeam>(c => c
            .Without(x => x.Vm)
            .With(x => x.TeamId, () => Guid.NewGuid())
            .With(x => x.VmId, () => Guid.NewGuid()));

        fixture.Customize<VmMap>(c => c
            .Without(x => x.Coordinates)
            .With(x => x.Id, () => Guid.NewGuid())
            .With(x => x.ViewId, () => Guid.NewGuid())
            .With(x => x.TeamIds, () => new List<Guid> { Guid.NewGuid() }));

        fixture.Customize<Coordinate>(c => c
            .With(x => x.Id, () => Guid.NewGuid())
            .With(x => x.Urls, () => Array.Empty<string>()));

        fixture.Customize<ProxmoxVmInfo>(c => c
            .With(x => x.VmId, () => Guid.NewGuid())
            .With(x => x.Type, () => ProxmoxVmType.QEMU));

        fixture.Customize<VmUser>(c => c
            .Without(x => x.LastVm)
            .With(x => x.UserId, () => Guid.NewGuid())
            .With(x => x.TeamId, () => Guid.NewGuid())
            .With(x => x.LastVmId, () => Guid.NewGuid())
            .With(x => x.LastSeen, () => DateTimeOffset.UtcNow));

        fixture.Customize<ConsoleConnectionInfo>(c => c);

        // --- VmLoggingContext entities ---

        fixture.Customize<VmUsageLoggingSession>(c => c
            .With(x => x.Id, () => Guid.NewGuid())
            .With(x => x.ViewId, () => Guid.NewGuid())
            .With(x => x.TeamIds, () => new[] { Guid.NewGuid() })
            .With(x => x.CreatedDt, () => DateTimeOffset.UtcNow)
            .With(x => x.SessionStart, () => DateTimeOffset.UtcNow)
            .With(x => x.SessionEnd, () => DateTimeOffset.UtcNow.AddHours(1)));

        fixture.Customize<VmUsageLogEntry>(c => c
            .Without(x => x.Session)
            .With(x => x.Id, () => Guid.NewGuid())
            .With(x => x.SessionId, () => Guid.NewGuid())
            .With(x => x.VmId, () => Guid.NewGuid())
            .With(x => x.UserId, () => Guid.NewGuid())
            .With(x => x.VmActiveDT, () => DateTimeOffset.UtcNow)
            .With(x => x.VmInactiveDT, () => DateTimeOffset.UtcNow.AddMinutes(30)));
    }
}
