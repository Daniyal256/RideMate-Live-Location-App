namespace RideMate.Application.DTOs.Auth;

public class RegisterDto
{
    public string DisplayName { get; set; }
        = string.Empty;

    public string Email { get; set; }
        = string.Empty;

    public string Password { get; set; }
        = string.Empty;

    public string PhoneNumber { get; set; }
        = string.Empty;
}