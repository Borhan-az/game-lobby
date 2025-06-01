using Lobby.Api.Hubs;
using Lobby.Application.Models;
using Lobby.Application.Services;
using Lobby.Contract.Interfaces;
using Lobby.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSignalR();


builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var redisConnectionString = configuration.GetConnectionString("RedisConnection");
    if (string.IsNullOrEmpty(redisConnectionString))
    {
        throw new InvalidOperationException("RedisConnection string is not configured.");
    }
    return ConnectionMultiplexer.Connect(redisConnectionString);
});

builder.Services.AddSingleton<RedLockFactory>(sp =>
{
    var connectionMultiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
    var redLockEndpoints = new List<RedLockEndPoint>
    {
        new RedLockEndPoint { EndPoint = connectionMultiplexer.GetEndPoints().First() }
    };
    return RedLockFactory.Create(redLockEndpoints);
});

builder.Services.AddSingleton<IRedisService, RedisService>();

builder.Services.AddSingleton<ILobbyService, LobbyService>();

builder.Services.Configure<LobbySetting>(builder.Configuration.GetSection("LobbySettings"));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapHub<ChatHub>("/chatHub");
app.UseStaticFiles();
app.Run();
