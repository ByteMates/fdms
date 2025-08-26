using ClosedXML.Excel;
using EmployeeService.Data;
using EmployeeService.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeService.Services
{
    public class EmployeeService : IEmployeeService
    {
        private readonly ApplicationDbContext _context;

        public EmployeeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Employee> AddEmployeeAsync(Employee employee)
        {
            employee.Id = await GenerateEmployeeIdAsync();
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();
            return employee;
        }

        private async Task<string> GenerateEmployeeIdAsync()
        {
            int count = await _context.Employees.CountAsync() + 1;
            return $"EMP-{count.ToString("D4")}";
        }

        public async Task<Employee?> UpdateEmployeeAsync(string id, Employee updated)
        {
            var existing = await _context.Employees.FindAsync(id);
            if (existing == null) return null;

            _context.Entry(existing).CurrentValues.SetValues(updated);
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteEmployeeAsync(string id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return false;

            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Employee?> GetEmployeeByIdAsync(string id)
        {
            return await _context.Employees.FindAsync(id);
        }

        public async Task<IEnumerable<Employee>> SearchEmployeesAsync(string? cnic, string? personnelNo, string? name)
        {
            return await _context.Employees
                .Where(e =>
                    (string.IsNullOrEmpty(cnic) || e.CNIC == cnic) &&
                    (string.IsNullOrEmpty(personnelNo) || e.PersonnelNumber == personnelNo) &&
                    (string.IsNullOrEmpty(name) || e.Name.Contains(name)))
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetAllEmployeesAsync()
        {
            return await _context.Employees.ToListAsync();
        }

        public async Task<List<Employee>> UploadEmployeesFromExcelAsync(Stream excelStream)
        {
            var workbook = new XLWorkbook(excelStream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            var result = new List<Employee>();

            if (worksheet == null)
                return result;

            int rowCount = worksheet.LastRowUsed().RowNumber();

            for (int row = 2; row <= rowCount; row++)
            {
                var employee = new Employee
                {
                    CNIC = worksheet.Cell(row, 1).GetString(),
                    Name = worksheet.Cell(row, 2).GetString(),
                    FatherName = worksheet.Cell(row, 3).GetString(),
                    Gender = worksheet.Cell(row, 4).GetString(),
                    DateOfBirth = TryParseDate(worksheet.Cell(row, 5).GetString()),
                    PersonnelNumber = worksheet.Cell(row, 6).GetString(),
                    BPS = worksheet.Cell(row, 7).GetString(),
                    ESGroup = worksheet.Cell(row, 8).GetString(),
                    Designation = worksheet.Cell(row, 9).GetString(),
                    Position = worksheet.Cell(row, 10).GetString(),
                    PayrollArea = worksheet.Cell(row, 11).GetString(),
                    CostCenterCode = worksheet.Cell(row, 12).GetString(),
                    CostCenter = worksheet.Cell(row, 13).GetString(),
                    FundCode = worksheet.Cell(row, 14).GetString(),
                    TypeOfEmployment = worksheet.Cell(row, 15).GetString(),
                    EmployeeOf = worksheet.Cell(row, 16).GetString(),
                    PrimaryDepartment = worksheet.Cell(row, 17).GetString(),
                    CurrentOffice = worksheet.Cell(row, 18).GetString(),
                    AppointmentDate = TryParseDate(worksheet.Cell(row, 19).GetString()),
                    RetirementDate = TryParseDate(worksheet.Cell(row, 20).GetString()),
                    ChargedDesignation = worksheet.Cell(row, 21).GetString(),
                    Wing = worksheet.Cell(row, 22).GetString(),
                    Section = worksheet.Cell(row, 23).GetString(),
                    CurrentStatus = worksheet.Cell(row, 24).GetString(),
                    Remarks = worksheet.Cell(row, 25).GetString()
                };

                if (string.IsNullOrWhiteSpace(employee.CNIC) || string.IsNullOrWhiteSpace(employee.Name))
                    continue;

                result.Add(employee);
            }

            _context.Employees.AddRange(result);
            await _context.SaveChangesAsync();

            return result;
        }

        private DateTime? TryParseDate(string input)
        {
            return DateTime.TryParse(input, out var parsed) ? parsed : null;
        }
    }
}
