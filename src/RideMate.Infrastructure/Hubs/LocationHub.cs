using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RideMate.Domain.Entities;
using RideMate.Infrastructure.Data;
using System.Collections.Concurrent;

namespace RideMate.Infrastructure.Hubs;

public class LocationHub : Hub
{
    private static readonly TimeSpan OfflineAfter = TimeSpan.FromMinutes(2);
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> UserConnections = new();
    private readonly AppDbContext _db;

    public LocationHub(AppDbContext db)
    {
        _db = db;
    }

    public override Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var connections = UserConnections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
        connections[Context.ConnectionId] = 0;

        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        var hasOtherConnections = false;

        if (UserConnections.TryGetValue(userId, out var connections))
        {
            connections.TryRemove(Context.ConnectionId, out _);
            hasOtherConnections = !connections.IsEmpty;

            if (connections.IsEmpty)
            {
                UserConnections.TryRemove(userId, out _);
            }
        }

        if (!hasOtherConnections)
        {
            await MarkUserOfflineNowAsync(userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinCircle(string circleId)
    {
        var userId = GetUserId();

        if (!Guid.TryParse(circleId, out var parsedCircleId))
            throw new HubException("Invalid circle id.");

        if (!await IsMemberAsync(parsedCircleId, userId))
            throw new HubException("You are not a member of this circle.");

        await Groups.AddToGroupAsync(Context.ConnectionId, circleId);

        var memberUserIds = await _db.CircleMembers
            .Where(cm => cm.CircleId == parsedCircleId)
            .Select(cm => cm.UserId)
            .ToListAsync();

        var latestLocations = (await _db.LocationLogs
            .Where(l => l.CircleId == parsedCircleId && memberUserIds.Contains(l.UserId))
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync())
            .DistinctBy(l => l.UserId)
            .ToList();

        foreach (var loc in latestLocations)
        {
            var user = await _db.Users
                .Where(u => u.Id == loc.UserId)
                .Select(u => new
                {
                    u.Id,
                    u.DisplayName,
                    u.AvatarUrl
                })
                .SingleOrDefaultAsync();

            if (user is null)
                continue;

            await SendLocationAsync(Clients.Caller, user.Id, user.DisplayName, user.AvatarUrl, loc);
        }
    }

    public async Task LeaveCircle(string circleId)
    {
        if (!Guid.TryParse(circleId, out _))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, circleId);
    }

    public async Task UpdateLocation(string circleId, double lat, double lng, int? batteryLevel)
    {
        var userId = GetUserId();

        if (!Guid.TryParse(circleId, out var parsedCircleId))
            return;

        if (!await IsMemberAsync(parsedCircleId, userId))
            return;

        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.DisplayName,
                u.AvatarUrl
            })
            .SingleOrDefaultAsync();

        if (user is null)
            return;

        var resolvedBatteryLevel = await ResolveBatteryLevelAsync(parsedCircleId, userId, batteryLevel);
        var timestamp = DateTime.UtcNow;

        _db.LocationLogs.Add(new LocationLog
        {
            CircleId = parsedCircleId,
            UserId = userId,
            Latitude = lat,
            Longitude = lng,
            BatteryLevel = resolvedBatteryLevel,
            IsLocationPermissionDenied = false,
            Timestamp = timestamp
        });

        await _db.SaveChangesAsync();

        await Clients.Group(circleId).SendAsync(
            "ReceiveLocation",
            user.Id,
            NormalizeDisplayName(user.DisplayName),
            NormalizeAvatar(user.AvatarUrl),
            lat,
            lng,
            timestamp,
            resolvedBatteryLevel,
            0);
    }

    public async Task ReportLocationUnavailable(
        string circleId,
        int? batteryLevel,
        double fallbackLat,
        double fallbackLng,
        bool isLocationPermissionDenied)
    {
        var userId = GetUserId();

        if (!Guid.TryParse(circleId, out var parsedCircleId))
            return;

        if (!await IsMemberAsync(parsedCircleId, userId))
            return;

        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.DisplayName,
                u.AvatarUrl
            })
            .SingleOrDefaultAsync();

        if (user is null)
            return;

        var lastLocation = await _db.LocationLogs
            .Where(l => l.CircleId == parsedCircleId && l.UserId == userId)
            .OrderByDescending(l => l.Timestamp)
            .FirstOrDefaultAsync();

        var lat = lastLocation?.Latitude ?? fallbackLat;
        var lng = lastLocation?.Longitude ?? fallbackLng;
        var resolvedBatteryLevel = NormalizeBatteryLevel(batteryLevel) ?? lastLocation?.BatteryLevel;
        var timestamp = DateTime.UtcNow;

        _db.LocationLogs.Add(new LocationLog
        {
            CircleId = parsedCircleId,
            UserId = userId,
            Latitude = lat,
            Longitude = lng,
            BatteryLevel = resolvedBatteryLevel,
            IsLocationPermissionDenied = isLocationPermissionDenied,
            Timestamp = timestamp
        });

        await _db.SaveChangesAsync();

        await Clients.Group(circleId).SendAsync(
            "ReceiveLocation",
            user.Id,
            NormalizeDisplayName(user.DisplayName),
            NormalizeAvatar(user.AvatarUrl),
            lat,
            lng,
            timestamp,
            resolvedBatteryLevel,
            isLocationPermissionDenied ? 2 : 1);
    }

    private string GetUserId()
    {
        var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();

        if (string.IsNullOrWhiteSpace(userId))
            throw new HubException("Missing user id.");

        return userId;
    }

    private Task<bool> IsMemberAsync(Guid circleId, string userId)
    {
        return _db.CircleMembers.AnyAsync(cm =>
            cm.CircleId == circleId &&
            cm.UserId == userId);
    }

    private static async Task SendLocationAsync(
        IClientProxy client,
        string userId,
        string displayName,
        string? avatarUrl,
        LocationLog loc)
    {
        var isOffline =
            loc.IsLocationPermissionDenied ||
            !IsUserOnline(userId) ||
            DateTime.UtcNow - loc.Timestamp.ToUniversalTime() > OfflineAfter;

        await client.SendAsync(
            "ReceiveLocation",
            userId,
            NormalizeDisplayName(displayName),
            NormalizeAvatar(avatarUrl),
            loc.Latitude,
            loc.Longitude,
            loc.Timestamp,
            loc.BatteryLevel,
            loc.IsLocationPermissionDenied ? 2 : isOffline ? 1 : 0);
    }

    private static string NormalizeDisplayName(string? displayName)
    {
        return string.IsNullOrWhiteSpace(displayName) ? "RideMate User" : displayName;
    }

    private static string NormalizeAvatar(string? avatarUrl)
    {
        return string.IsNullOrWhiteSpace(avatarUrl) ? "/favicon.png" : avatarUrl;
    }

    private static int? NormalizeBatteryLevel(int? batteryLevel)
    {
        return batteryLevel is null ? null : Math.Clamp(batteryLevel.Value, 0, 100);
    }

    private async Task<int?> ResolveBatteryLevelAsync(Guid circleId, string userId, int? batteryLevel)
    {
        var normalized = NormalizeBatteryLevel(batteryLevel);

        if (normalized is not null)
            return normalized;

        return await _db.LocationLogs
            .Where(l => l.CircleId == circleId && l.UserId == userId && l.BatteryLevel != null)
            .OrderByDescending(l => l.Timestamp)
            .Select(l => l.BatteryLevel)
            .FirstOrDefaultAsync();
    }

    public static bool IsUserOnline(string userId)
    {
        return UserConnections.TryGetValue(userId, out var connections) &&
            !connections.IsEmpty;
    }

    private async Task MarkUserOfflineNowAsync(string userId)
    {
        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.DisplayName,
                u.AvatarUrl
            })
            .SingleOrDefaultAsync();

        if (user is null)
        {
            return;
        }

        var circleIds = await _db.CircleMembers
            .Where(cm => cm.UserId == userId)
            .Select(cm => cm.CircleId)
            .ToListAsync();

        foreach (var circleId in circleIds)
        {
            var lastLocation = await _db.LocationLogs
                .Where(l => l.CircleId == circleId && l.UserId == userId)
                .OrderByDescending(l => l.Timestamp)
                .FirstOrDefaultAsync();

            if (lastLocation is null)
            {
                continue;
            }

            var offlineLocation = new LocationLog
            {
                CircleId = circleId,
                UserId = userId,
                Latitude = lastLocation.Latitude,
                Longitude = lastLocation.Longitude,
                BatteryLevel = lastLocation.BatteryLevel,
                IsLocationPermissionDenied = lastLocation.IsLocationPermissionDenied,
                Timestamp = DateTime.UtcNow
            };

            _db.LocationLogs.Add(offlineLocation);

            await Clients.Group(circleId.ToString()).SendAsync(
                "ReceiveLocation",
                user.Id,
                NormalizeDisplayName(user.DisplayName),
                NormalizeAvatar(user.AvatarUrl),
                offlineLocation.Latitude,
                offlineLocation.Longitude,
                offlineLocation.Timestamp,
                offlineLocation.BatteryLevel,
                offlineLocation.IsLocationPermissionDenied ? 2 : 1);
        }

        await _db.SaveChangesAsync();
    }
}
