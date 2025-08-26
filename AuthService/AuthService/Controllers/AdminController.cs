using AuthService.Data;
using AuthService.DTOs;
using AuthService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // restrict access via policy if needed
    public class AdminController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // GET /api/admin/users?system=HRMIS&role=Admin&username=bilal
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] string? system, [FromQuery] string? role, [FromQuery] string? username)
        {
            var query = _userManager.Users
                .Include(u => u.UserSystemRoles)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(username))
                query = query.Where(u => u.UserName.Contains(username));

            if (!string.IsNullOrWhiteSpace(system) || !string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(u => u.UserSystemRoles.Any(r =>
                    (string.IsNullOrEmpty(system) || r.SystemCode == system) &&
                    (string.IsNullOrEmpty(role) || r.RoleName == role)));
            }

            var users = await query.ToListAsync();

            var result = users.Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email,
                u.FullName,
                u.CNIC,
                u.PersonnelNumber,
                u.AuthProvider,
                Roles = u.UserSystemRoles.Select(r => new { r.SystemCode, r.RoleName, r.IsActive })
            });

            return Ok(result);
        }

        // GET /api/admin/users/{id}
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUserById(string id)
        {
            var user = await _userManager.Users
                .Include(u => u.UserSystemRoles)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFound();

            return Ok(new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.FullName,
                user.CNIC,
                user.PersonnelNumber,
                user.AuthProvider,
                Roles = user.UserSystemRoles.Select(r => new { r.SystemCode, r.RoleName, r.IsActive })
            });
        }

        // PUT /api/admin/users/{id}
        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.FullName = request.FullName ?? user.FullName;
            user.Email = request.Email ?? user.Email;
            user.UserName = request.UserName ?? user.UserName;

            var result = await _userManager.UpdateAsync(user);
            return result.Succeeded ? Ok("User updated.") : BadRequest(result.Errors);
        }


        // PATCH /api/admin/users/{id}/deactivate
        [HttpPatch("users/{id}/deactivate")]
        public async Task<IActionResult> DeactivateUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;

            await _userManager.UpdateAsync(user);
            return Ok("User deactivated.");
        }


        // PATCH /api/admin/users/{id}/reactivate
        [HttpPatch("users/{id}/reactivate")]
        public async Task<IActionResult> ReactivateUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.LockoutEnd = null;
            await _userManager.UpdateAsync(user);
            return Ok("User reactivated.");
        }


        // DELETE /api/admin/users/{id}
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            await _userManager.DeleteAsync(user);
            return Ok("User deleted.");
        }


        // POST /api/admin/users/{id}/add-role
        [HttpPost("users/{id}/add-role")]
        public async Task<IActionResult> AddUserRole(string id, [FromBody] AddUserRoleRequest input)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (!await _roleManager.RoleExistsAsync(input.RoleName))
                await _roleManager.CreateAsync(new IdentityRole(input.RoleName));

            await _userManager.AddToRoleAsync(user, input.RoleName);

            var exists = await _context.UserSystemRoles.AnyAsync(r =>
                r.UserId == user.Id && r.SystemCode == input.SystemCode && r.RoleName == input.RoleName);

            if (!exists)
            {
                _context.UserSystemRoles.Add(new UserSystemRole
                {
                    UserId = user.Id,
                    SystemCode = input.SystemCode,
                    RoleName = input.RoleName,
                    IsActive = true
                });

                await _context.SaveChangesAsync();
            }

            return Ok("Role added.");
        }


        // DELETE /api/admin/users/{id}/remove-role
        [HttpDelete("users/{id}/remove-role")]
        public async Task<IActionResult> RemoveUserRole(string id, [FromBody] RemoveUserRoleRequest input)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            await _userManager.RemoveFromRoleAsync(user, input.RoleName);

            var mapping = await _context.UserSystemRoles.FirstOrDefaultAsync(r =>
                r.UserId == user.Id && r.SystemCode == input.SystemCode && r.RoleName == input.RoleName);

            if (mapping != null)
            {
                _context.UserSystemRoles.Remove(mapping);
                await _context.SaveChangesAsync();
            }

            return Ok("Role removed.");
        }

    }
}
