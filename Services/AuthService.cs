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
    Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request);
    Task<AuthResponse> ChangePasswordAsync(ChangePasswordRequest request, string userId);
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
        try
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
                Console.WriteLine($"DEBUG: Validating referral code: {request.ReferredBy}");
                var referrer = await _userRepository.GetUserByReferralCodeAsync(request.ReferredBy);
                if (referrer == null)
                {
                    Console.WriteLine($"DEBUG: Invalid referral code: {request.ReferredBy}");
                    return new AuthResponse { Success = false, Message = "Invalid referral code." };
                }
                Console.WriteLine($"DEBUG: Valid referral code found for user: {referrer.Name}");
            }

            Console.WriteLine($"DEBUG: Creating user with ReferredBy: '{request.ReferredBy}'");

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
                ReferredBy = request.ReferredBy ?? string.Empty,
                HostingAreas = request.HostingAreas ?? string.Empty
            };

            Console.WriteLine($"DEBUG: User entity ReferredBy before save: '{user.ReferredBy}'");

            var createdUser = await _userRepository.CreateUserAsync(user);

            Console.WriteLine($"DEBUG: User entity ReferredBy after save: '{createdUser.ReferredBy}'");

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
                User = MapToUserDto(createdUser),
                Token = GenerateToken(createdUser.RowKey, createdUser.Name)
            };
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Registration failed: {ex.Message}" };
        }
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
            Token = GenerateToken(user.RowKey, user.Name)
        };
    }

    public async Task<AuthResponse> UpdateUserAsync(UpdateUserRequest request)
    {
        var user = await _userRepository.GetUserByIdAsync(request.UserId);
        if (user == null)
        {
            return new AuthResponse { Success = false, Message = "User not found." };
        }

        if (!string.IsNullOrEmpty(request.Bio))
            user.Bio = request.Bio;
        
        if (request.HostingAreas != null)
            user.HostingAreas = request.HostingAreas;

        var updatedUser = await _userRepository.UpdateUserAsync(user);

        return new AuthResponse
        {
            Success = true,
            Message = "User updated successfully.",
            User = MapToUserDto(updatedUser)
        };
    }

    public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        try
        {
            // Validate OTP
            var otpValid = await _emailService.ValidateOTPAsync(new ValidateOTPRequest 
            { 
                Email = request.Email, 
                OTPCode = request.OtpCode 
            });
            
            if (!otpValid.Success)
            {
                return new AuthResponse { Success = false, Message = "Invalid or expired OTP." };
            }

            // Get user by email
            var user = await _userRepository.GetUserByEmailAsync(request.Email.ToLower());
            if (user == null)
            {
                return new AuthResponse { Success = false, Message = "User not found." };
            }

            // Update password
            user.Password = HashPassword(request.NewPassword);
            await _userRepository.UpdateUserAsync(user);

            return new AuthResponse
            {
                Success = true,
                Message = "Password reset successfully."
            };
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Password reset failed: {ex.Message}" };
        }
    }

    public async Task<AuthResponse> ChangePasswordAsync(ChangePasswordRequest request, string userId)
    {
        try
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
            {
                return new AuthResponse { Success = false, Message = "User not found." };
            }

            // Verify current password
            if (!VerifyPassword(request.CurrentPassword, user.Password))
            {
                return new AuthResponse { Success = false, Message = "Current password is incorrect." };
            }

            // Update password
            user.Password = HashPassword(request.NewPassword);
            await _userRepository.UpdateUserAsync(user);

            return new AuthResponse
            {
                Success = true,
                Message = "Password changed successfully."
            };
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Password change failed: {ex.Message}" };
        }
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

    private string GenerateToken(string userId, string userName = "")
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_secrets.JwtSecretKey);
        
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("userId", userId)
        };

        if (!string.IsNullOrEmpty(userName))
        {
            claims.Add(new Claim("name", userName));
        }
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
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
        List<HostingAreaDto>? hostingAreas = null;
        if (!string.IsNullOrEmpty(user.HostingAreas))
        {
            try
            {
                Console.WriteLine($"DEBUG: Raw HostingAreas from DB: {user.HostingAreas}");
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                hostingAreas = System.Text.Json.JsonSerializer.Deserialize<List<HostingAreaDto>>(user.HostingAreas, options);
                Console.WriteLine($"DEBUG: Parsed HostingAreas count: {hostingAreas?.Count}");
                // Filter out empty entries
                hostingAreas = hostingAreas?.Where(h => !string.IsNullOrEmpty(h.State) || h.Cities.Any()).ToList();
                Console.WriteLine($"DEBUG: Filtered HostingAreas count: {hostingAreas?.Count}");
                if (hostingAreas?.Count == 0) hostingAreas = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Error parsing HostingAreas: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("DEBUG: HostingAreas is empty or null in DB");
        }

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
            ReferralCode = user.ReferralCode,
            ReferredBy = user.ReferredBy,
            HostingAreas = hostingAreas
        };
    }
}