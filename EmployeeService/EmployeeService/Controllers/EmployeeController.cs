using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EmployeeService.Services;
using EmployeeService.Models;


namespace EmployeeService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmployeeController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    public EmployeeController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    [HttpPost("add")]
    public async Task<IActionResult> AddEmployee([FromBody] Employee employee)
    {
        if (string.IsNullOrEmpty(employee.CNIC) || string.IsNullOrEmpty(employee.Name))
            return BadRequest("CNIC and Name are required.");

        var created = await _employeeService.AddEmployeeAsync(employee);
        return Ok(created);
    }

    [HttpPut("update/{id}")]
    public async Task<IActionResult> UpdateEmployee(string id, [FromBody] Employee updated)
    {
        var result = await _employeeService.UpdateEmployeeAsync(id, updated);
        if (result == null)
            return NotFound("Employee not found");

        return Ok(result);
    }

    [HttpDelete("delete/{id}")]
    public async Task<IActionResult> DeleteEmployee(string id)
    {
        var deleted = await _employeeService.DeleteEmployeeAsync(id);
        if (!deleted)
            return NotFound("Employee not found or already deleted");

        return Ok("Deleted successfully");
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetEmployeeById(string id)
    {
        var employee = await _employeeService.GetEmployeeByIdAsync(id);
        if (employee == null)
            return NotFound("Employee not found");

        return Ok(employee);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchEmployees(
        [FromQuery] string? cnic,
        [FromQuery] string? personnelNo,
        [FromQuery] string? name)
    {
        var results = await _employeeService.SearchEmployeesAsync(cnic, personnelNo, name);
        return Ok(results);
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAllEmployees()
    {
        var results = await _employeeService.GetAllEmployeesAsync();
        return Ok(results);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadEmployeesFromExcel(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Excel file is required");

        using var stream = file.OpenReadStream();
        var importedEmployees = await _employeeService.UploadEmployeesFromExcelAsync(stream);
        return Ok(importedEmployees);
    }
}

