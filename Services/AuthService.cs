using FestiveGuestAPI.DTOs;
using FestiveGuestAPI.Models;
using FestiveGuestAPI.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace FestiveGuestAPI.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> UpdateUserAsync(UpdateUserRequest request);
}

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly AppSecrets _secrets;

    public AuthService(IUserRepository userRepository, IEmailService emailService, AppSecrets secrets)
    {
        _userRepository = userRepository;
        _emailService = emailService;
        _secrets = secrets;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // Validate user type
        if (request.UserType != "Guest" && request.UserType != "Host")
        {
            return new AuthResponse { Success = false, Message = "Invalid user type. Must be 'Guest' or 'Host'." };
        }

        // Check if email already exists
        var existingUser = await _userRepository.GetUserByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return new AuthResponse { Success = false, Message = "Email already registered." };
        }

        // Validate phone format (basic validation)
        if (!IsValidPhone(request.Phone))
        {
            return new AuthResponse { Success = false, Message = "Invalid phone number format." };
        }

        // Validate referral code if provided
        if (!string.IsNullOrWhiteSpace(request.ReferredBy))
        {
            var referrer = await _userRepository.GetUserByReferralCodeAsync(request.ReferredBy);
            if (referrer == null)
            {
                return new AuthResponse { Success = false, Message = "Invalid referral code." };
            }
        }

        // Create user entity
        var user = new UserEntity
        {
            Name = request.Name,
            Email = request.Email.ToLower(),
            Phone = request.Phone,
            Password = HashPassword(request.Password),
            UserType = request.UserType,
            Location = request.Location,
            Bio = request.Bio,
            ReferredBy = request.ReferredBy ?? string.Empty
        };

        var createdUser = await _userRepository.CreateUserAsync(user);
        
        // Generate and save referral code
        createdUser.ReferralCode = GenerateReferralCode(createdUser.RowKey);
        await _userRepository.UpdateUserAsync(createdUser);

        // Send registration confirmation email
        try
        {
            await _emailService.SendRegistrationConfirmationAsync(createdUser.Email, createdUser.Name);
        }
        catch
        {
            // Log error but don't fail registration
        }

        return new AuthResponse
        {
            Success = true,
            Message = "User registered successfully.",
            User = MapToUserDto(createdUser)
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetUserByEmailAsync(request.Email.ToLower());
        if (user == null)
        {
            return new AuthResponse { Success = false, Message = "Invalid email or password." };
        }

        if (!VerifyPassword(request.Password, user.Password))
        {
            return new AuthResponse { Success = false, Message = "Invalid email or password." };
        }

        if (user.Status != "Active")
        {
            return new AuthResponse { Success = false, Message = "Account is not active." };
        }

        return new AuthResponse
        {
            Success = true,
            Message = "Login successful.",
            User = MapToUserDto(user),
            Token = GenerateToken(user.RowKey)
        };
    }

    public async Task<AuthResponse> UpdateUserAsync(UpdateUserRequest request)
    {
        var user = await _userRepository.GetUserByIdAsync(request.UserId);
        if (user == null)
        {
            return new AuthResponse { Success = false, Message = "User not found." };
        }

        // Update only provided fields
        if (!string.IsNullOrEmpty(request.Name))
            user.Name = request.Name;
        
        if (!string.IsNullOrEmpty(request.Phone))
        {
            if (!IsValidPhone(request.Phone))
            {
                return new AuthResponse { Success = false, Message = "Invalid phone number format." };
            }
            user.Phone = request.Phone;
        }
        
        if (!string.IsNullOrEmpty(request.Location))
            user.Location = request.Location;
        
        if (!string.IsNullOrEmpty(request.Bio))
            user.Bio = request.Bio;

        var updatedUser = await _userRepository.UpdateUserAsync(user);

        return new AuthResponse
        {
            Success = true,
            Message = "User updated successfully.",
            User = MapToUserDto(updatedUser)
        };
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "FestiveGuest2024"));
        return Convert.ToBase64String(hashedBytes);
    }

    private bool VerifyPassword(string password, string hashedPassword)
    {
        return HashPassword(password) == hashedPassword;
    }

    private bool IsValidPhone(string phone)
    {
        return phone.Length >= 10 && phone.All(char.IsDigit);
    }

    private string GenerateReferralCode(string userId)
    {
        var random = new Random();
        var randomPart = random.Next(1000, 9999);
        return $"FG{userId.Substring(0, Math.Min(5, userId.Length))}{randomPart}".ToUpper();
    }

    private string GenerateToken(string userId)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_secrets.JwtSecretKey);
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim("userId", userId)
            }),
            Expires = DateTime.UtcNow.AddDays(30), // 30 days expiration
            Issuer = _secrets.JwtIssuer,
            Audience = _secrets.JwtAudience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private UserDto MapToUserDto(UserEntity user)
    {
        return new UserDto
        {
            UserId = user.RowKey,
            Name = user.Name,
            Email = user.Email,
            Phone = user.Phone,
            UserType = user.UserType,
            Status = user.Status,
            Location = user.Location,
            Bio = user.Bio,
            ProfileImageUrl = user.ProfileImageUrl,
            IsVerified = user.IsVerified,
            CreatedDate = user.CreatedDate,
            ReferralCode = user.ReferralCode
        };
    }
}