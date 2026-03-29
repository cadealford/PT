using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PetroTransit.API.DTOs;
using PetroTransit.API.Models;
using PetroTransit.API.Services;

namespace PetroTransit.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;

    public AuthController(UserManager<AppUser> userManager, IConfiguration configuration, IEmailService emailService)
    {
        _userManager = userManager;
        _configuration = configuration;
        _emailService = emailService;
    }

    [HttpPost("forgot-username")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotUsername([FromBody] ForgotRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Ok("If an account exists for that email, the username has been sent.");
        }

        var subject = "PetroTransit Username Recovery";
        var body = $"Hello {user.FullName},\n\nYour username for PetroTransit is: **{user.UserName}**\n\nThank you.";

        await _emailService.SendEmailAsync(user.Email!, subject, body);

        return Ok("If an account exists for that email, the username has been sent.");
    }

    // --- DEV: Test Email Endpoint ---
    [HttpPost("test-email")]
    [AllowAnonymous]
    public async Task<IActionResult> TestEmail([FromBody] TestEmailRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.ToEmail))
        {
            return BadRequest("ToEmail is required.");
        }

        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? "PetroTransit Test Email"
            : request.Subject.Trim();
        var body = string.IsNullOrWhiteSpace(request.Body)
            ? "This is a test email sent from PetroTransit."
            : request.Body.Trim();

        await _emailService.SendEmailAsync(request.ToEmail, subject, body);

        return Ok("Test email sent.");
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Ok("If an account exists for that email, a password reset link has been sent.");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        
        var frontendBaseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";
        var resetLink = $"{frontendBaseUrl}/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";

        var subject = "PetroTransit Password Reset Request";
        var body = $"Hello {user.FullName},\n\nYou have requested a password reset. Please click the link below to reset your password:\n\n{resetLink}\n\nIf you did not request this, please ignore this email.";

        await _emailService.SendEmailAsync(user.Email!, subject, body);

        return Ok("If an account exists for that email, a password reset link has been sent.");
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return BadRequest("Invalid request.");
        }

        // Tokens passed via querystring can have '+' decoded as space; normalize before use.
        var normalizedToken = request.Token.Replace(' ', '+');
        var result = await _userManager.ResetPasswordAsync(user, normalizedToken, request.NewPassword);

        if (result.Succeeded)
        {
            return Ok("Your password has been reset successfully.");
        }

        return BadRequest(result.Errors);
    }
    
    [HttpGet("users")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<IEnumerable<UserListDto>>> GetUsers()
    {
        var users = await _userManager.Users
            .Select(u => new UserListDto
            {
                Id = u.Id,
                UserName = u.UserName ?? "",
                FullName = u.FullName,
                IsAdmin = u.IsAdmin
            })
            .ToListAsync();

        return Ok(users);
    }

    // Existing /register endpoint is used for adding new users
    [HttpPost("register")]
    [AllowAnonymous] // Admins use this to create accounts. AllowAnonymous is fine since the endpoint handles its own authentication flow for regular users/new accounts.
    public async Task<ActionResult<AppUser>> Register(UserRegisterDto request)
    {
        var user = new AppUser
        {
            UserName = request.Username,
            Email = request.Email,
            FullName = request.FullName
        };
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(result.Errors);
        return Ok("User registered successfully.");
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<string>> Login(UserLoginDto request)
    {
        var user = await _userManager.FindByNameAsync(request.Username);
        if (user == null) return Unauthorized("Invalid credentials.");

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized("Invalid credentials.");
        }

        List<Claim> claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.UserName ?? ""), 
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim("isAdmin", user.IsAdmin.ToString().ToLowerInvariant())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration.GetSection("AppSettings:Token").Value!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.Now.AddDays(1),
            signingCredentials: creds
        );

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(new { token = jwt });
    }
    
    [HttpPut("promote/{userId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> PromoteUserToAdmin(string userId)
    {
        var targetUser = await _userManager.FindByIdAsync(userId);
        if (targetUser == null) return NotFound("User not found.");

        targetUser.IsAdmin = !targetUser.IsAdmin; // Toggles the IsAdmin flag
        var result = await _userManager.UpdateAsync(targetUser);

        if (!result.Succeeded) return BadRequest(result.Errors);

        return Ok($"User {targetUser.UserName}'s admin status has been toggled.");
    }
    
    // --- NEW: DELETE USER ENDPOINT ---
    [HttpDelete("delete/{userId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound("User not found.");
        }
        
        var result = await _userManager.DeleteAsync(user);
        
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }
        
        return NoContent(); // 204 success
    }
}
