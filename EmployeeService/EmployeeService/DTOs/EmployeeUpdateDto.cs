using System.ComponentModel.DataAnnotations;

namespace EmployeeService.DTOs;

public class EmployeeUpdateDto
{
    public string? PersonnelNumber { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    public string CNIC { get; set; }

    public string? FatherName { get; set; }
    public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? BPS { get; set; }
    public string? ESGroup { get; set; }
    public string? Designation { get; set; }
    public string? Position { get; set; }
    public string? PayrollArea { get; set; }
    public string? CostCenterCode { get; set; }
    public string? CostCenter { get; set; }
    public string? FundCode { get; set; }
    public string? TypeOfEmployment { get; set; }
    public string? EmployeeOf { get; set; }
    public string? PrimaryDepartment { get; set; }
    public string? CurrentOffice { get; set; }
    public DateTime? AppointmentDate { get; set; }
    public DateTime? RetirementDate { get; set; }
    public string? ChargedDesignation { get; set; }
    public string? Wing { get; set; }
    public string? Section { get; set; }
    public string? CurrentStatus { get; set; }
    public string? Remarks { get; set; }
}
