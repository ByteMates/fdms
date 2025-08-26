using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using AuthService.Controllers;
using AuthService.DTOs;
using AuthService.Models;
using AuthService.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
        private readonly Mock<RoleManager<IdentityRole>> _roleManagerMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ApplicationDbContext> _dbContextMock;

        public AuthControllerTests()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);

            var roleStore = new Mock<IRoleStore<IdentityRole>>();
            _roleManagerMock = new Mock<RoleManager<IdentityRole>>(roleStore.Object, null, null, null, null);

            _configurationMock = new Mock<IConfiguration>();
            _dbContextMock = new Mock<ApplicationDbContext>();
        }

        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }


        [Fact]
        public async Task RegisterSystemUser_ReturnsOk_WhenValid()
        {
            var dbContext = GetInMemoryDbContext();

            var controller = new AuthController(
                _userManagerMock.Object,
                _roleManagerMock.Object,
                dbContext,
                _configurationMock.Object
            );


            var request = new RegisterRequest
            {
                Username = "medadmin",
                Password = "Admin@123",
                FullName = "Admin User",
                Role = "Admin",
                SystemCode = "SYS"
            };

            _userManagerMock.Setup(x => x.FindByNameAsync(request.Username))
                .ReturnsAsync((ApplicationUser)null);
            _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
                .ReturnsAsync(IdentityResult.Success);
            _roleManagerMock.Setup(x => x.RoleExistsAsync(request.Role))
                .ReturnsAsync(true);
            _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), request.Role))
                .ReturnsAsync(IdentityResult.Success);

            var result = await controller.RegisterSystemUser(request);

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task RegisterSystemUser_ReturnsBadRequest_WhenUserAlreadyExists()
        {
            var dbContext = GetInMemoryDbContext();

            var controller = new AuthController(
                _userManagerMock.Object,
                _roleManagerMock.Object,
                dbContext,
                _configurationMock.Object
            );

            var request = new RegisterRequest
            {
                Username = "medadmin",
                Password = "Admin@123",
                Role = "Admin",
                SystemCode = "SYS"
            };

            _userManagerMock.Setup(x => x.FindByNameAsync(request.Username))
                .ReturnsAsync(new ApplicationUser());

            var result = await controller.RegisterSystemUser(request);

            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
 