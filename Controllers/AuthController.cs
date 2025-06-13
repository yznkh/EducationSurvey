using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SocSurvey2.Models;
using BCrypt.Net;
using SocSurvey2.Data;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly SurveyContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(SurveyContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        var admin = await _context.Admins
            .FirstOrDefaultAsync(a => a.Username == model.Username);

        if (admin == null || !BCrypt.Net.BCrypt.Verify(model.Password, admin.PasswordHash))
        {
            return Unauthorized(new { message = "اسم المستخدم أو كلمة المرور غير صحيحة" });
        }

        var token = GenerateJwtToken(admin);
        return Ok(new { token });
    }

    private string GenerateJwtToken(Admin admin)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new Claim(ClaimTypes.Name, admin.Username)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["Jwt:ExpiryMinutes"])),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class LoginModel
{
    public string Username { get; set; }
    public string Password { get; set; }
}