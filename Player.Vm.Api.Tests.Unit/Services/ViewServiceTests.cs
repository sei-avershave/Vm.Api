// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using FakeItEasy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Player.Vm.Api.Domain.Services;
using Player.Vm.Api.Infrastructure.Options;
using Shouldly;
using Xunit;

namespace Player.Vm.Api.Tests.Unit.Services;

[Trait("Category", "Unit")]
public class ViewServiceTests
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<ViewService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ClientOptions _clientOptions;

    public ViewServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _logger = A.Fake<ILogger<ViewService>>();
        _httpClientFactory = A.Fake<IHttpClientFactory>();

        _clientOptions = new ClientOptions
        {
            urls = new ApiUrlSettings
            {
                playerApi = "http://localhost:4300/"
            }
        };

        // Return a real HttpClient from the factory for construction
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:4300/") };
        A.CallTo(() => _httpClientFactory.CreateClient("player-admin")).Returns(httpClient);
    }

    private ViewService CreateSut()
    {
        return new ViewService(_httpClientFactory, _cache, _clientOptions, _logger);
    }

    [Fact]
    public void Constructor_WithValidOptions_CreatesInstance()
    {
        // Act
        var sut = CreateSut();

        // Assert
        sut.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetTeamsForView_WhenCached_ReturnsCachedResult()
    {
        // Arrange
        var viewId = Guid.NewGuid();
        var teamIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        // Pre-populate the cache to test caching behavior
        _cache.Set(viewId, teamIds, new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15)));

        var sut = CreateSut();

        // Act
        var result = await sut.GetTeamsForView(viewId, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldBe(teamIds);
    }

    [Fact]
    public async Task GetViewIdForTeam_WhenTeamInfoCached_ReturnsViewId()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var viewId = Guid.NewGuid();

        var teamInfo = new TeamInfo
        {
            TeamName = "Test Team",
            ViewId = viewId,
            ViewName = "Test View"
        };

        // Pre-populate the cache
        _cache.Set(teamId, teamInfo, new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15)));

        var sut = CreateSut();

        // Act
        var result = await sut.GetViewIdForTeam(teamId, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(viewId);
    }

    [Fact]
    public async Task GetViewIdsForTeams_WhenTeamsCached_ReturnsDistinctViewIds()
    {
        // Arrange
        var viewId = Guid.NewGuid();
        var teamId1 = Guid.NewGuid();
        var teamId2 = Guid.NewGuid();

        var teamInfo1 = new TeamInfo { TeamName = "Team 1", ViewId = viewId, ViewName = "View 1" };
        var teamInfo2 = new TeamInfo { TeamName = "Team 2", ViewId = viewId, ViewName = "View 1" };

        _cache.Set(teamId1, teamInfo1, new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15)));
        _cache.Set(teamId2, teamInfo2, new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15)));

        var sut = CreateSut();

        // Act
        var result = await sut.GetViewIdsForTeams(new[] { teamId1, teamId2 }, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Length.ShouldBe(1);
        result[0].ShouldBe(viewId);
    }
}
