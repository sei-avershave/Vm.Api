// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Crucible.Common.Testing.Auth;
using Crucible.Common.Testing.Extensions;
using FakeItEasy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Player.Vm.Api.Data;
using Player.Vm.Api.Domain.Proxmox.Services;
using Player.Vm.Api.Domain.Services;
using Player.Vm.Api.Domain.Vsphere.Services;
using Player.Vm.Api.Infrastructure.Authorization;
using Testcontainers.PostgreSql;
using Xunit;

namespace Player.Vm.Api.Tests.Integration.Fixtures;

/// <summary>
/// WebApplicationFactory for Player VM API integration tests.
/// Uses Testcontainers PostgreSQL and handles both VmContext and VmLoggingContext.
/// </summary>
public class VmTestContext : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseEnvironment("Test")
            .UseSetting("Database:Provider", "PostgreSQL")
            .UseSetting("Database:AutoMigrate", "false")
            .UseSetting("Database:DevModeRecreate", "false")
            .UseSetting("VmUsageLogging:Enabled", "false")
            .UseSetting("VmUsageLogging:PostgreSQL", _container!.GetConnectionString())
            .UseSetting("HealthChecksUI:Enabled", "false")
            .UseSetting("Authorization:Authority", "http://localhost:5000")
            .UseSetting("Authorization:AuthorizationScope", "player player-vm")
            .UseSetting("Authorization:PrivilegedScope", "player-vm-privileged")
            .UseSetting("Authorization:RequireHttpsMetadata", "false")
            .UseSetting("ClientSettings:urls:playerApi", "http://localhost:4300/")
            .UseSetting("IdentityClient:TokenUrl", "http://localhost:5000/connect/token")
            .UseSetting($"ConnectionStrings:PostgreSQL", _container!.GetConnectionString())
            .ConfigureServices(services =>
            {
                if (_container is null)
                    throw new InvalidOperationException("Database container has not been started.");

                var connectionString = _container.GetConnectionString();

                // Remove existing VmContext registrations (AddEventPublishingDbContextFactory registers multiple)
                services.RemoveServices<IDbContextFactory<VmContext>>();
                services.RemoveService<VmContext>();

                // Re-register VmContext with test database
                services.AddDbContext<VmContext>(options =>
                    options.UseNpgsql(connectionString));

                // Remove and replace VmLoggingContext
                services.RemoveService<VmLoggingContext>();

                services.AddDbContext<VmLoggingContext>(options =>
                    options.UseNpgsql(connectionString));

                // Replace authentication with test handler
                services
                    .AddAuthentication(TestAuthenticationHandler.AuthenticationSchemeName)
                    .AddScheme<TestAuthenticationHandlerOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.AuthenticationSchemeName, _ => { });

                // Replace claims transformation
                services.ReplaceService<IClaimsTransformation, TestClaimsTransformation>(allowMultipleReplace: true);

                // Replace authorization service to allow everything through
                services.ReplaceService<IAuthorizationService, TestAuthorizationService>();

                // Replace external services with fakes
                var fakePlayerService = A.Fake<IPlayerService>();
                A.CallTo(() => fakePlayerService.Can(
                    A<IEnumerable<Guid>>._, A<IEnumerable<Guid>>._,
                    A<AppSystemPermission[]>._, A<AppViewPermission[]>._, A<AppTeamPermission[]>._,
                    A<CancellationToken>._))
                    .Returns(true);
                A.CallTo(() => fakePlayerService.CanViewTeams(
                    A<IEnumerable<Guid>>._, A<CancellationToken>._))
                    .Returns(true);
                A.CallTo(() => fakePlayerService.CanManageTeams(
                    A<IEnumerable<Guid>>._, A<CancellationToken>._))
                    .Returns(true);
                A.CallTo(() => fakePlayerService.CanEditTeams(
                    A<IEnumerable<Guid>>._, A<CancellationToken>._))
                    .Returns(true);

                services.ReplaceService<IPlayerService, IPlayerService>(fakePlayerService);
                services.ReplaceService<IViewService, IViewService>(A.Fake<IViewService>());

                // Replace singleton hosted services with fakes to avoid real connections
                services.RemoveServices<ConnectionService>();
                services.RemoveServices<IConnectionService>();
                services.AddSingleton(A.Fake<IConnectionService>());

                services.RemoveServices<TaskService>();
                services.RemoveServices<ITaskService>();
                services.AddSingleton(A.Fake<ITaskService>());

                services.RemoveServices<MachineStateService>();
                services.RemoveServices<IMachineStateService>();
                services.AddSingleton(A.Fake<IMachineStateService>());

                services.RemoveServices<ProxmoxStateService>();
                services.RemoveServices<IProxmoxStateService>();
                services.AddSingleton(A.Fake<IProxmoxStateService>());

                services.RemoveServices<CallbackBackgroundService>();
                services.RemoveServices<ICallbackBackgroundService>();
                services.AddSingleton(A.Fake<ICallbackBackgroundService>());
            });
    }

    /// <summary>
    /// Gets a scoped VmContext for direct database validation in tests.
    /// </summary>
    public VmContext GetDbContext()
    {
        return Services.GetRequiredService<VmContext>();
    }

    /// <summary>
    /// Gets a scoped VmLoggingContext for direct database validation in tests.
    /// </summary>
    public VmLoggingContext GetLoggingDbContext()
    {
        return Services.GetRequiredService<VmLoggingContext>();
    }

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithHostname("localhost")
            .WithUsername("crucible")
            .WithPassword("crucible")
            .WithImage("postgres:latest")
            .WithAutoRemove(true)
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();

        // Ensure both databases are created
        using var scope = Services.CreateScope();

        var vmContext = scope.ServiceProvider.GetRequiredService<VmContext>();
        await vmContext.Database.EnsureCreatedAsync();

        var loggingContext = scope.ServiceProvider.GetRequiredService<VmLoggingContext>();
        await loggingContext.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
