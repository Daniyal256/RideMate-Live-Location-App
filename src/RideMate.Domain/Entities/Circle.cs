using System.ComponentModel.DataAnnotations;

namespace RideMate.Domain.Entities;

public class Circle
{
    [Key]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string InviteCode { get; set; } = string.Empty;

    public string CreatorId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CircleMember> Members { get; set; } = new List<CircleMember>();
}

public class CircleMember
{
    public Guid CircleId { get; set; }

    public Circle Circle { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
