# Vm.Api Readme

The Vm.Api is the backend restful API for the VM application that integrates with Player to display and manage virtual machines.

## Testing

This project uses [TUnit](https://tunit.dev/) as its test framework with FakeItEasy for mocking.

### Test Projects

| Project | Description |
|---------|-------------|
| `Player.Vm.Api.Tests.Unit` | Unit tests for services using in-memory EF Core and FakeItEasy |
| `Player.Vm.Api.Tests.Integration` | Integration tests with WebApplicationFactory and Testcontainers PostgreSQL |
| `Player.Vm.Api.Tests.Shared` | Shared AutoFixture customizations for entity types |

### Running Tests

```bash
# Run all tests
dotnet test

# Run unit tests only
dotnet test Player.Vm.Api.Tests.Unit

# Run integration tests (requires Docker)
dotnet test Player.Vm.Api.Tests.Integration
```

## Reporting bugs and requesting features

Think you found a bug? Please report all Crucible bugs - including bugs for the individual Crucible apps - in the [cmu-sei/crucible issue tracker](https://github.com/cmu-sei/crucible/issues).

Include as much detail as possible including steps to reproduce, specific app involved, and any error messages you may have received.

Have a good idea for a new feature? Submit all new feature requests through the [cmu-sei/crucible issue tracker](https://github.com/cmu-sei/crucible/issues).

Include the reasons why you're requesting the new feature and how it might benefit other Crucible users.

## License

Copyright 2022 Carnegie Mellon University. See the [LICENSE.md](./LICENSE.md) files for details.
