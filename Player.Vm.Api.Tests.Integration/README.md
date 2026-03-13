# Player.Vm.Api.Tests.Integration

Copyright 2026 Carnegie Mellon University. All Rights Reserved.

Integration tests for Player VM API using Testcontainers PostgreSQL and WebApplicationFactory.

## Purpose

This project contains integration tests that validate the VM API's end-to-end functionality, including HTTP endpoints, database operations, and system integration. Tests run against a real PostgreSQL database in a Docker container.

## Files

### Fixtures

- **`Fixtures/VmTestContext.cs`** - WebApplicationFactory<Program> with Testcontainers PostgreSQL. Handles both VmContext and VmLoggingContext, replaces authentication with test handlers, and mocks external services (IPlayerService, IViewService, IConnectionService, ITaskService, IMachineStateService, IProxmoxStateService).

### Tests

- **`Tests/Controllers/HealthCheckTests.cs`** - Health endpoint tests (`/api/health/live`, `/api/health/ready`)
- **`Tests/Controllers/VmControllerTests.cs`** - VM API endpoint tests:
  - `GetAll_ReturnsOkAndEmptyList_WhenNoVmsExist`
  - `CreateVm_ReturnsCreated_WithValidForm`
  - `GetVm_ReturnsOk_WhenVmExists`
  - `DeleteVm_ReturnsNoContent_WhenVmExists`
  - `GetTeamVms_ReturnsOk_WhenTeamHasVms`

## Key Patterns

### WebApplicationFactory Setup

```csharp
public class VmTestContext : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseEnvironment("Test")
            .UseSetting("ConnectionStrings:PostgreSQL", _container!.GetConnectionString())
            .ConfigureServices(services =>
            {
                // Replace VmContext with test database
                services.RemoveServices<IDbContextFactory<VmContext>>();
                services.RemoveService<VmContext>();
                services.AddDbContext<VmContext>(options =>
                    options.UseNpgsql(connectionString));

                // Replace authentication
                services
                    .AddAuthentication(TestAuthenticationHandler.AuthenticationSchemeName)
                    .AddScheme<TestAuthenticationHandlerOptions, TestAuthenticationHandler>(...);

                // Mock external services
                var fakePlayerService = A.Fake<IPlayerService>();
                A.CallTo(() => fakePlayerService.Can(...)).Returns(true);
                services.ReplaceService<IPlayerService, IPlayerService>(fakePlayerService);
            });
    }

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithHostname("localhost")
            .WithUsername("crucible")
            .WithPassword("crucible")
            .WithImage("postgres:latest")
            .Build();

        await _container.StartAsync();

        // Ensure both databases are created
        var vmContext = Services.GetRequiredService<VmContext>();
        await vmContext.Database.EnsureCreatedAsync();

        var loggingContext = Services.GetRequiredService<VmLoggingContext>();
        await loggingContext.Database.EnsureCreatedAsync();
    }
}
```

### Controller Testing

```csharp
public class VmControllerTests : IClassFixture<VmTestContext>
{
    private readonly HttpClient _client;
    private readonly VmTestContext _factory;

    [Fact]
    public async Task CreateVm_ReturnsCreated_WithValidForm()
    {
        // Arrange
        var form = new VmCreateForm
        {
            Id = Guid.NewGuid(),
            Name = "integration-test-vm",
            TeamIds = new List<Guid> { Guid.NewGuid() }
        };

        // Act
        var response = await _client.PostAsync("/api/vms", form.ToJsonBody());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createdVm = JsonSerializer.Deserialize<Vm>(
            await response.Content.ReadAsStringAsync(), JsonOptions);
        createdVm.ShouldNotBeNull();

        // Verify in database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VmContext>();
        var dbVm = await context.Vms
            .Include(v => v.VmTeams)
            .FirstOrDefaultAsync(v => v.Id == form.Id);
        dbVm.ShouldNotBeNull();
    }
}
```

### Database Verification

```csharp
[Fact]
public async Task GetVm_ReturnsOk_WhenVmExists()
{
    // Arrange - seed database
    using (var scope = _factory.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<VmContext>();
        var vm = new VmEntity
        {
            Id = vmId,
            Name = "get-test-vm",
            PowerState = PowerState.On,
            Type = VmType.Vsphere,
            VmTeams = new List<VmTeam> { new(teamId, vmId) }
        };
        context.Vms.Add(vm);
        await context.SaveChangesAsync();
    }

    // Act
    var response = await _client.GetAsync($"/api/vms/{vmId}");

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
}
```

## Dependencies

- **xUnit** - Test framework
- **Shouldly** - Fluent assertions
- **FakeItEasy** - Mocking framework for external services
- **Microsoft.AspNetCore.Mvc.Testing** - WebApplicationFactory for in-process testing
- **Testcontainers.PostgreSql** - Docker container for real PostgreSQL database
- **Npgsql.EntityFrameworkCore.PostgreSQL** - PostgreSQL EF Core provider
- **Crucible.Common.Testing** - Shared testing utilities (TestAuthenticationHandler, TestClaimsTransformation, extension methods)
- **Player.Vm.Api.Tests.Shared** - Shared fixtures

## Running Tests

```bash
# Run all integration tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~VmControllerTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~VmControllerTests.CreateVm_ReturnsCreated_WithValidForm"

# With verbose output
dotnet test --logger "console;verbosity=detailed"

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Prerequisites

- **Docker** must be running for Testcontainers
- Tests automatically start and stop PostgreSQL containers
- Each test run uses a fresh database instance

## Test Structure

### IClassFixture Pattern

```csharp
public class VmControllerTests : IClassFixture<VmTestContext>
{
    private readonly VmTestContext _factory;
    private readonly HttpClient _client;

    public VmControllerTests(VmTestContext factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }
}
```

xUnit creates one VmTestContext instance per test class, shared across all test methods.

### IAsyncLifetime

```csharp
public async Task InitializeAsync()
{
    // Start container and create databases before any tests run
    await _container.StartAsync();
    await vmContext.Database.EnsureCreatedAsync();
}

public async Task DisposeAsync()
{
    // Clean up container after all tests complete
    await _container.DisposeAsync();
}
```

## JSON Serialization

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNameCaseInsensitive = true,
    Converters = { new JsonStringEnumConverter() }
};

var vm = JsonSerializer.Deserialize<Vm>(content, JsonOptions);
```

## Mocked Services

All external services are mocked to prevent real connections:

- **IPlayerService** - Permission checks always return true
- **IViewService** - View/team relationship lookups
- **IConnectionService** - vSphere/Proxmox connections
- **ITaskService** - VM task management
- **IMachineStateService** - vSphere state tracking
- **IProxmoxStateService** - Proxmox state tracking
- **ICallbackBackgroundService** - Background callbacks

This allows tests to focus on API logic without requiring real virtualization platforms.

## Database Contexts

VmTestContext manages two separate databases:

1. **VmContext** - Main VM data (Vms, VmTeams, VmMaps, Coordinates)
2. **VmLoggingContext** - Usage logging (VmUsageLoggingSessions, VmUsageLogEntries)

Both use the same PostgreSQL container with separate schemas/tables.

## Extension Methods

From `Crucible.Common.Testing.Extensions`:

```csharp
// Convert object to JSON HttpContent
var content = form.ToJsonBody();

// Remove service registrations
services.RemoveService<VmContext>();
services.RemoveServices<IConnectionService>();  // Removes all registrations

// Replace service
services.ReplaceService<IPlayerService, IPlayerService>(fakeService);
```

## Related Projects

- **Player.Vm.Api.Tests.Unit** - Fast unit tests with in-memory database
- **Player.Vm.Api.Tests.Shared** - Shared fixtures
- **Player.Vm.Api** - API under test
