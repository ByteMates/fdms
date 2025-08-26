using EmployeeService.DTOs;
using EmployeeService.Models;

namespace EmployeeService.Services;

public interface IEmployeeService
{
    Task<Employee> AddEmployeeAsync(Employee employee);
    Task<Employee?> UpdateEmployeeAsync(string id, Employee updated);
    Task<bool> DeleteEmployeeAsync(string id);
    Task<Employee?> GetEmployeeByIdAsync(string id);
    Task<IEnumerable<Employee>> SearchEmployeesAsync(string? cnic, string? personnelNo, string? name);
    Task<IEnumerable<Employee>> GetAllEmployeesAsync();
    Task<List<Employee>> UploadEmployeesFromExcelAsync(Stream excelFileStream);
}
