using System.ComponentModel.DataAnnotations;

namespace FestiveGuestAPI.DTOs;

public class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;
    
    [Required, MinLength(2)]
    public string Name { get; set; } = string.Empty;
    
    [Required, Phone]
    public string Phone { get; set; } = string.Empty;
    
    [Required]
    public string UserType { get; set; } = string.Empty; // Guest/Host
    
    public string Location { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string Password { get; set; } = string.Empty;
}

public class UpdateUserRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [MinLength(2)]
    public string? Name { get; set; }
    
    [Phone]
    public string? Phone { get; set; }
    
    public string? Location { get; set; }
    public string? Bio { get; set; }
}

public class AuthResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public UserDto? User { get; set; }
    public string? Token { get; set; }
}

public class UserDto
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string ProfileImageUrl { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public DateTime CreatedDate { get; set; }
}