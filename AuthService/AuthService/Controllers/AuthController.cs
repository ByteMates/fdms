using AuthService.Data;
using AuthService.DTOs;
using AuthService.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            IConfiguration config)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _config = config;
        }

        [HttpPost("register/system")]
        public async Task<IActionResult> RegisterSystemUser(RegisterRequest request)
        {
            var existingUser = await _userManager.FindByNameAsync(request.Username);
            if (existingUser != null)
                return BadRequest("User already exists");

            var user = new ApplicationUser
            {
                UserName = request.Username,
                FullName = request.FullName,
                AuthProvider = "Local"
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            if (!await _roleManager.RoleExistsAsync(request.Role))
                await _roleManager.CreateAsync(new IdentityRole(request.Role));

            await _userManager.AddToRoleAsync(user, request.Role);

            // Save mapping in UserSystemRole table
            var mapping = new UserSystemRole
            {
                UserId = user.Id,
                SystemCode = request.SystemCode,
                RoleName = request.Role
            };

            _context.UserSystemRoles.Add(mapping);
            await _context.SaveChangesAsync();

            return Ok("User registered successfully.");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            ApplicationUser user = await _context.Users
                .Include(u => u.UserSystemRoles)
                .FirstOrDefaultAsync(u =>
                    u.UserName == request.LoginId ||
                    u.CNIC == request.LoginId ||
                    u.PersonnelNumber == request.LoginId);

            if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
                return Unauthorized("Invalid credentials");

            var roles = await _userManager.GetRolesAsync(user);
            var userSystems = await _context.UserSystemRoles
                .Where(r => r.UserId == user.Id && r.IsActive)
                .ToListAsync();

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim("username", user.UserName),
                new Claim("fullName", user.FullName),
                new Claim("authProvider", user.AuthProvider)
            };

            foreach (var system in userSystems)
            {
                claims.Add(new Claim("system", system.SystemCode));
                claims.Add(new Claim("role", $"{system.SystemCode}:{system.RoleName}"));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(4),
                signingCredentials: creds);

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expiresIn = 14400,
                user = new
                {
                    user.Id,
                    user.UserName,
                    user.FullName,
                    Roles = userSystems.Select(r => new { r.SystemCode, r.RoleName })
                }
            });
        }

        [HttpPost("register/employee")]
        public async Task<IActionResult> RegisterEmployee(RegisterEmployeeRequest request)
        {
            // At least CNIC or PersonnelNumber must be provided
            if (string.IsNullOrWhiteSpace(request.CNIC) && string.IsNullOrWhiteSpace(request.PersonnelNumber))
                return BadRequest("Either CNIC or Personnel Number is required.");

            // Check if CNIC or PersonnelNumber already exists
            var existingUser = await _userManager.Users.FirstOrDefaultAsync(u =>
                u.CNIC == request.CNIC || u.PersonnelNumber == request.PersonnelNumber);

            if (existingUser != null)
                return BadRequest("An account with this CNIC or Personnel Number already exists.");

            var user = new ApplicationUser
            {
                UserName = Guid.NewGuid().ToString("N").Substring(0, 12), // Auto-generated username
                CNIC = request.CNIC,
                PersonnelNumber = request.PersonnelNumber,
                FullName = request.FullName,
                AuthProvider = "Local"
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            var roleName = "Employee";

            if (!await _roleManager.RoleExistsAsync(roleName))
                await _roleManager.CreateAsync(new IdentityRole(roleName));

            await _userManager.AddToRoleAsync(user, roleName);

            // Map system + role
            var mapping = new UserSystemRole
            {
                UserId = user.Id,
                SystemCode = request.SystemCode,
                RoleName = roleName
            };

            _context.UserSystemRoles.Add(mapping);
            await _context.SaveChangesAsync();

            return Ok("Employee registered successfully.");
        }

        [HttpGet("login/google")]
        public IActionResult LoginWithGoogle([FromQuery] string returnUrl = "/api/auth/google-callback")
        {
            var redirectUrl = Url.Action("GoogleCallback", "Auth", new { returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };

            return Challenge(properties, "Google"); // Google provider name
        }

        [HttpGet("google-callback")]
        public async Task<IActionResult> GoogleCallback(string returnUrl = "/")
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded)
                return Unauthorized();

            var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
            var fullName = result.Principal.FindFirst(ClaimTypes.Name)?.Value;

            // Check if user exists in your DB
            var user = await _userManager.Users
                .Include(u => u.UserSystemRoles)
                .FirstOrDefaultAsync(u => u.Email == email && u.AuthProvider == "Google");

            if (user == null)
            {
                user = new ApplicationUser
                {
                    Email = email,
                    FullName = fullName,
                    AuthProvider = "Google",
                    UserName = Guid.NewGuid().ToString("N").Substring(0, 12),
                    EmailConfirmed = true
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                    return BadRequest(createResult.Errors);

                var roleName = "ExternalUser";
                if (!await _roleManager.RoleExistsAsync(roleName))
                    await _roleManager.CreateAsync(new IdentityRole(roleName));

                await _userManager.AddToRoleAsync(user, roleName);

                _context.UserSystemRoles.Add(new UserSystemRole
                {
                    UserId = user.Id,
                    SystemCode = "EmployeePortal", // or use `returnUrl` param to map system
                    RoleName = roleName
                });

                await _context.SaveChangesAsync();
            }

            // Create your own JWT
            var roles = await _userManager.GetRolesAsync(user);
            var userSystems = await _context.UserSystemRoles
                .Where(r => r.UserId == user.Id && r.IsActive)
                .ToListAsync();

            var claims = new List<Claim>
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id),
        new Claim("username", user.UserName),
        new Claim("fullName", user.FullName ?? ""),
        new Claim("email", user.Email),
        new Claim("authProvider", "Google")
    };

            foreach (var system in userSystems)
            {
                claims.Add(new Claim("system", system.SystemCode));
                claims.Add(new Claim("role", $"{system.SystemCode}:{system.RoleName}"));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var jwt = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(4),
                signingCredentials: creds);

            var token = new JwtSecurityTokenHandler().WriteToken(jwt);

            // You can either return it directly or redirect with the token
            return Ok(new
            {
                token,
                user = new
                {
                    user.UserName,
                    user.Email,
                    user.FullName,
                    Systems = userSystems.Select(r => new { r.SystemCode, r.RoleName })
                }
            });
        }


    }
}
