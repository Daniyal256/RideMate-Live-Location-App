using System.ComponentModel.DataAnnotations;

namespace RideMate.Domain.Entities;

public class LocationLog
{
    [Key]
    public long Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public Guid CircleId { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double Speed { get; set; }

    public int? BatteryLevel { get; set; }

    public bool IsLocationPermissionDenied { get; set; }

    public DateTime Timestamp { get; set; }
        = DateTime.UtcNow;
}
