# Player.Vm.Api.Tests.Shared

Copyright 2026 Carnegie Mellon University. All Rights Reserved.

Shared test fixtures and utilities for Player VM API testing.

## Purpose

This project provides reusable AutoFixture customizations for VM domain entities, preventing circular reference issues from Entity Framework navigation properties. Used across both unit and integration tests.

## Files

- **`Fixtures/VmCustomization.cs`** - AutoFixture customization that registers factories for all Player VM entity types (Vm, VmTeam, VmMap, Coordinate, ProxmoxVmInfo, VmUser, ConsoleConnectionInfo, VmUsageLoggingSession, VmUsageLogEntry). Handles entities for both VmContext and VmLoggingContext.

## Domain Models

All fixtures use the `Player.Vm.Api.Domain.Models` namespace:

- **VmContext entities**: Vm, VmTeam, VmMap, Coordinate, ProxmoxVmInfo, VmUser, ConsoleConnectionInfo
- **VmLoggingContext entities**: VmUsageLoggingSession, VmUsageLogEntry

## Usage

Apply the customization to your AutoFixture instance:

```csharp
var fixture = new Fixture()
    .Customize(new VmCustomization());

// Create test VM entities
var vm = fixture.Create<Vm>();
var vmTeam = fixture.Create<VmTeam>();
var vmMap = fixture.Create<VmMap>();
```

The customization:
- Removes `ThrowingRecursionBehavior` and adds `OmitOnRecursionBehavior` to handle circular references
- Uses `Without()` to exclude navigation properties (e.g., `Vm.VmTeams`, `VmTeam.Vm`)
- Sets sensible defaults for enums (PowerState.On, VmType.Vsphere, ProxmoxVmType.QEMU)
- Initializes collections and arrays (AllowedNetworks, IpAddresses, TeamIds, Urls)
- Generates valid GUIDs for all ID properties

## Dependencies

- **AutoFixture** - Data generation and customization
- **Player.Vm.Api** - VM domain models
- **Crucible.Common.Testing** - Shared testing utilities

## Running Tests

This is a shared library project with no tests of its own. It is consumed by:

- `Player.Vm.Api.Tests.Unit`
- `Player.Vm.Api.Tests.Integration`

## Key Patterns

**Avoiding Circular References**:
```csharp
fixture.Customize<VmEntity>(c => c
    .Without(x => x.VmTeams)  // Navigation property
    .With(x => x.Id, () => Guid.NewGuid()));
```

**Setting Collection Defaults**:
```csharp
fixture.Customize<VmMap>(c => c
    .Without(x => x.Coordinates)
    .With(x => x.TeamIds, () => new List<Guid> { Guid.NewGuid() }));
```

**Enum Defaults**:
```csharp
fixture.Customize<VmEntity>(c => c
    .With(x => x.PowerState, () => PowerState.On)
    .With(x => x.Type, () => VmType.Vsphere));
```

This customization is used across both unit and integration tests to generate test data with AutoFixture.
