# Player.Vm.Api.Tests.Unit

Copyright 2026 Carnegie Mellon University. All Rights Reserved.

Unit tests for Player VM API services, mappers, and business logic.

## Purpose

This project contains unit tests for the VM API's core functionality, including AutoMapper configuration validation, service layer operations, and permission enforcement. Tests use in-memory databases and mocked dependencies for fast, isolated execution.

## Files

- **`MappingConfigurationTests.cs`** - AutoMapper profile validation with ConsoleUrlResolver dependency. Uses ConstructServicesUsing to provide ConsoleUrlOptions and FakeItEasy for other service types. Tests entity-to-DTO mapping (VmEntity->Vm, VmCreateForm->VmEntity, VmMap->VmMapDto).
- **`Services/ViewServiceTests.cs`** - ViewService tests for caching team-to-view relationships and retrieving team/view metadata
- **`Services/VmServiceTests.cs`** - VmService CRUD operation tests with permission validation (GetAllAsync, CreateAsync, DeleteAsync)

## Key Patterns

### AutoMapper Testing

The mapping tests are the most detailed of all Crucible APIs, testing individual entity-to-DTO conversions:

```csharp
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
```

### Service Testing with TestDbContextFactory

```csharp
[Fact]
public async Task GetAllAsync_WithViewPermission_ReturnsAllVms()
{
    // Arrange
    using var context = TestDbContextFactory.Create<VmContext>();
    var vmEntities = _fixture.CreateMany<VmEntity>(3).ToList();

    context.Vms.AddRange(vmEntities);
    context.SaveChanges();

    A.CallTo(() => _fakePlayerService.Can(...)).Returns(true);

    var sut = CreateSut(context);

    // Act
    var result = await sut.GetAllAsync(CancellationToken.None);

    // Assert
    result.Length.ShouldBe(3);
}
```

### Permission Testing

```csharp
[Fact]
public async Task DeleteAsync_WithoutPermission_ThrowsForbidden()
{
    // Arrange
    using var context = TestDbContextFactory.Create<VmContext>();
    var vmEntity = _fixture.Create<VmEntity>();

    context.Vms.Add(vmEntity);
    context.SaveChanges();

    A.CallTo(() => _fakePlayerService.CanManageTeams(...)).Returns(false);

    var sut = CreateSut(context);

    // Act & Assert
    await Should.ThrowAsync<ForbiddenException>(
        () => sut.DeleteAsync(vmEntity.Id, CancellationToken.None));
}
```

## Dependencies

- **xUnit** - Test framework
- **Shouldly** - Fluent assertions
- **FakeItEasy** - Mocking framework
- **AutoFixture** - Test data generation
- **AutoFixture.AutoFakeItEasy** - Automatic fake generation
- **Microsoft.EntityFrameworkCore.InMemory** - In-memory database for testing
- **MockQueryable.FakeItEasy** - IQueryable mocking
- **Crucible.Common.Testing** - Shared testing utilities (TestDbContextFactory)
- **Player.Vm.Api.Tests.Shared** - Shared fixtures (VmCustomization)

## Running Tests

```bash
# Run all unit tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~MappingConfigurationTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~VmServiceTests.GetAllAsync_WithViewPermission_ReturnsAllVms"

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Test Structure

### Arrange-Act-Assert Pattern

All tests follow the AAA pattern:

```csharp
[Fact]
public async Task Method_Condition_ExpectedBehavior()
{
    // Arrange - set up test data, mocks, and system under test
    var vmId = Guid.NewGuid();
    var sut = CreateSut(context);

    // Act - execute the method being tested
    var result = await sut.GetAsync(vmId, CancellationToken.None);

    // Assert - verify the outcome
    result.ShouldNotBeNull();
    result.Id.ShouldBe(vmId);
}
```

### Test Naming Convention

`MethodName_Condition_ExpectedBehavior`:
- `GetAllAsync_WithViewPermission_ReturnsAllVms`
- `CreateAsync_WithValidForm_ReturnsCreatedVm`
- `DeleteAsync_WithoutPermission_ThrowsForbidden`

## Mocking Guidelines

### FakeItEasy Syntax

```csharp
// Configure return values
A.CallTo(() => _fakePlayerService.Can(...)).Returns(true);

// Match any arguments of specific type
A.CallTo(() => _fakeService.Method(A<Guid>._, A<CancellationToken>._))
    .Returns(result);

// Verify calls
A.CallTo(() => _fakeService.Method(expectedId)).MustHaveHappened();
```

### AutoFixture with FakeItEasy

```csharp
private readonly IFixture _fixture = new Fixture()
    .Customize(new AutoFakeItEasyCustomization())
    .Customize(new VmCustomization());

// Create entities with automatic fake navigation properties
var vm = _fixture.Create<VmEntity>();
```

## Related Projects

- **Player.Vm.Api.Tests.Shared** - Shared fixtures
- **Player.Vm.Api.Tests.Integration** - Integration tests with real database
- **Player.Vm.Api** - API under test
