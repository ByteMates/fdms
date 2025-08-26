using Xunit;
using FluentAssertions;
using EmployeeService.Models;
using EmployeeService.Services;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using EmployeeService.Data;

namespace EmployeeServiceTests.Services
{
    public class EmployeeServiceTests
    {
        private async Task<ApplicationDbContext> GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return context;
        }

        [Fact]
        public async Task AddEmployeeAsync_ShouldAssignEmployeeId()
        {
            // Arrange
            var context = await GetInMemoryDbContext();
            var service = new EmployeeService.Services.EmployeeService(context);

            var employee = new Employee
            {
                CNIC = "1234567890123",
                Name = "Ali Khan"
            };

            // Act
            var result = await service.AddEmployeeAsync(employee);

            // Assert
            result.Id.Should().StartWith("EMP-");
            result.CNIC.Should().Be("1234567890123");
        }

        [Fact]
        public async Task GetEmployeeByIdAsync_ShouldReturnEmployee()
        {
            var context = await GetInMemoryDbContext();
            var service = new EmployeeService.Services.EmployeeService(context);

            var added = await service.AddEmployeeAsync(new Employee
            {
                CNIC = "321",
                Name = "John Doe"
            });

            var result = await service.GetEmployeeByIdAsync(added.Id);

            result.Should().NotBeNull();
            result?.Name.Should().Be("John Doe");
        }

        [Fact]
        public async Task SearchEmployeesAsync_ShouldReturnMatchingEmployee()
        {
            var context = await GetInMemoryDbContext();
            var service = new EmployeeService.Services.EmployeeService(context);

            await service.AddEmployeeAsync(new Employee
            {
                CNIC = "456",
                Name = "Sameer"
            });

            var results = await service.SearchEmployeesAsync("456", null, null);

            results.Should().HaveCount(1);
            results.First().Name.Should().Be("Sameer");
        }

    }
}
