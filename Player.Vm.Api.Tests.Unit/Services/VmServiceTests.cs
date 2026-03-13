// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using AutoFixture;
using AutoFixture.AutoFakeItEasy;
using AutoMapper;
using Crucible.Common.Testing.Fixtures;
using FakeItEasy;
using Player.Vm.Api.Data;
using Player.Vm.Api.Domain.Models;
using Player.Vm.Api.Domain.Services;
using Player.Vm.Api.Features.Vms;
using Player.Vm.Api.Infrastructure.Authorization;
using Player.Vm.Api.Infrastructure.Exceptions;
using Player.Vm.Api.Tests.Shared.Fixtures;
using Shouldly;
using VmEntity = Player.Vm.Api.Domain.Models.Vm;
using System.Security.Claims;
using System.Security.Principal;
using Xunit;

namespace Player.Vm.Api.Tests.Unit.Services;

public class VmServiceTests
{
    private readonly IFixture _fixture;
    private readonly IPlayerService _fakePlayerService;
    private readonly IMapper _fakeMapper;
    private readonly ClaimsPrincipal _user;
    private readonly Guid _userId = Guid.NewGuid();

    public VmServiceTests()
    {
        _fixture = new Fixture()
            .Customize(new AutoFakeItEasyCustomization())
            .Customize(new VmCustomization());

        _fakePlayerService = A.Fake<IPlayerService>();
        _fakeMapper = A.Fake<IMapper>();

        var claims = new List<Claim>
        {
            new("sub", _userId.ToString()),
            new(ClaimTypes.NameIdentifier, _userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        _user = new ClaimsPrincipal(identity);
    }

    private VmService CreateSut(VmContext context)
    {
        return new VmService(context, _fakePlayerService, _user, _fakeMapper);
    }

    [Fact]
    public async Task GetAllAsync_WithViewPermission_ReturnsAllVms()
    {
        // Arrange
        using var context = TestDbContextFactory.Create<VmContext>();
        var vmEntities = _fixture.CreateMany<VmEntity>(3).ToList();
        foreach (var vm in vmEntities)
        {
            vm.VmTeams = new List<VmTeam>
            {
                new VmTeam(Guid.NewGuid(), vm.Id)
            };
        }

        context.Vms.AddRange(vmEntities);
        context.SaveChanges();

        A.CallTo(() => _fakePlayerService.Can(
            A<IEnumerable<Guid>>._, A<IEnumerable<Guid>>._,
            A<AppSystemPermission[]>._, A<AppViewPermission[]>._, A<AppTeamPermission[]>._,
            A<CancellationToken>._))
            .Returns(true);

        var expectedVms = vmEntities.Select(v => new Features.Vms.Vm { Id = v.Id }).ToArray();
        A.CallTo(() => _fakeMapper.Map<Features.Vms.Vm[]>(A<object>._)).Returns(expectedVms);

        var sut = CreateSut(context);

        // Act
        var result = await sut.GetAllAsync(CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Length.ShouldBe(3);
    }

    [Fact]
    public async Task GetAllAsync_WithoutPermission_ThrowsForbidden()
    {
        // Arrange
        using var context = TestDbContextFactory.Create<VmContext>();
        var sut = CreateSut(context);

        A.CallTo(() => _fakePlayerService.Can(
            A<IEnumerable<Guid>>._, A<IEnumerable<Guid>>._,
            A<AppSystemPermission[]>._, A<AppViewPermission[]>._, A<AppTeamPermission[]>._,
            A<CancellationToken>._))
            .Returns(false);

        // Act & Assert
        await Should.ThrowAsync<ForbiddenException>(
            () => sut.GetAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WithValidForm_ReturnsCreatedVm()
    {
        // Arrange
        using var context = TestDbContextFactory.Create<VmContext>();
        var teamId = Guid.NewGuid();
        var vmId = Guid.NewGuid();

        var form = new VmCreateForm
        {
            Id = vmId,
            Name = "test-vm",
            TeamIds = new List<Guid> { teamId }
        };

        var vmEntity = _fixture.Create<VmEntity>();
        vmEntity.Id = vmId;
        vmEntity.VmTeams = new List<VmTeam> { new VmTeam(teamId, vmId) };

        A.CallTo(() => _fakeMapper.Map<VmEntity>(form)).Returns(vmEntity);
        A.CallTo(() => _fakePlayerService.CanManageTeams(
            A<IEnumerable<Guid>>._, A<CancellationToken>._))
            .Returns(true);

        var expectedVm = new Features.Vms.Vm { Id = vmId, Name = "test-vm" };
        A.CallTo(() => _fakeMapper.Map<Features.Vms.Vm>(vmEntity)).Returns(expectedVm);

        var sut = CreateSut(context);

        // Act
        var result = await sut.CreateAsync(form, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(vmId);
    }

    [Fact]
    public async Task DeleteAsync_WithoutPermission_ThrowsForbidden()
    {
        // Arrange
        using var context = TestDbContextFactory.Create<VmContext>();
        var vmId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var vmEntity = _fixture.Create<VmEntity>();
        vmEntity.Id = vmId;
        vmEntity.VmTeams = new List<VmTeam> { new VmTeam(teamId, vmId) };

        context.Vms.Add(vmEntity);
        context.SaveChanges();

        A.CallTo(() => _fakePlayerService.CanManageTeams(
            A<IEnumerable<Guid>>._, A<CancellationToken>._))
            .Returns(false);

        var sut = CreateSut(context);

        // Act & Assert
        await Should.ThrowAsync<ForbiddenException>(
            () => sut.DeleteAsync(vmId, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_VmNotFound_ThrowsEntityNotFound()
    {
        // Arrange
        using var context = TestDbContextFactory.Create<VmContext>();
        var vmId = Guid.NewGuid();

        var sut = CreateSut(context);

        // Act & Assert
        await Should.ThrowAsync<EntityNotFoundException<Features.Vms.Vm>>(
            () => sut.DeleteAsync(vmId, CancellationToken.None));
    }
}
