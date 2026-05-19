namespace RideMate.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    public string Email { get; set; }
        = string.Empty;

    public string DisplayName { get; set; }
        = string.Empty;

    public string? AvatarUrl { get; set; }

    public string? PhoneNumber { get; set; }

    public string PasswordHash { get; set; }
        = string.Empty;

    public bool IsVerified { get; set; } = false;

    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;

}
