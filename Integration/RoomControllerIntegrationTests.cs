﻿using System.Net.Http.Json;
using AutoFixture;
using Infrastructure;
using Xunit;
using Contracts.Input;
using Contracts.Output;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Integration;

public class RoomControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncDisposable
{
    private const string RoomEndpoint = "/Room";
    
    private readonly WebApplicationFactory<Program> _factory;
    private readonly IFixture _fixture;
    private readonly HttpClient _client;

    public RoomControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(ConfigureTestServices);
        _client = _factory.CreateClient();
        _fixture = new Fixture();
    }

    [Fact]
    public async Task CreateRoom_ReturnsCreatedRoom()
    {
        // Arrange
        var request = _fixture.Create<CreateRoomRequest>();

        // Act
        var response = await _client.PostAsJsonAsync(RoomEndpoint, request);

        // Assert
        response.EnsureSuccessStatusCode();
        var roomResponse = await response.Content.ReadFromJsonAsync<RoomOut>();
        
        roomResponse.Should().NotBeNull();
        roomResponse.Id.Should().NotBeEmpty();
        roomResponse.Code.Should().NotBeEmpty();
        roomResponse.Status.Should().Be(RoomOut.RoomStatus.Lobby);

        roomResponse.Players.Should().ContainSingle()
            .Which.Should().Match<PlayerOut>(
                p => p.Id.Length != 0 && 
                     p.Id != Guid.Empty.ToString() && 
                     p.Name == request.PlayerName);
        
        roomResponse.Host.Should().Match<PlayerOut>(h => 
            h.Id == roomResponse.Players[0].Id && 
            h.Name == request.PlayerName);
    }

    private static void ConfigureTestServices(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<RoomDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<RoomDbContext>(options =>
            {
                options.UseInMemoryDatabase("InMemoryDbForTesting")
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });
        });
    }
    
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

}
