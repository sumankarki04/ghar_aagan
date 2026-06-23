using GharAagan.Data;
using GharAagan.Dtos;
using GharAagan.Models;
using GharAagan.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GharAagan.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokens;

    public AuthController(AppDbContext db, TokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        // Reject undefined enum values (client could post any int).
        if (!Enum.IsDefined(typeof(UserRole), req.Role))
            return BadRequest("Invalid role.");
        // Admin accounts cannot be self-registered.
        if (req.Role == UserRole.Admin)
            return BadRequest("Admin accounts cannot be created through registration.");

        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict("An account with this email already exists.");

        var (hash, salt) = PasswordHasher.Hash(req.Password);
        var user = new User
        {
            FullName = req.FullName.Trim(),
            Email = email,
            PasswordHash = hash,
            PasswordSalt = salt,
            Phone = req.Phone,
            Role = req.Role,
            Bio = req.Role == UserRole.Provider ? req.Bio : null
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(BuildAuth(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !PasswordHasher.Verify(req.Password, user.PasswordHash, user.PasswordSalt))
            return Unauthorized("Invalid email or password.");

        return Ok(BuildAuth(user));
    }

    private AuthResponse BuildAuth(User user)
    {
        var (token, expires) = _tokens.CreateToken(user);
        return new AuthResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role.ToString(),
            Token = token,
            ExpiresAt = expires
        };
    }
}
