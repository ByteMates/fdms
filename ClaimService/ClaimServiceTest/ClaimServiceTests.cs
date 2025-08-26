using System.Security.Claims;
using ClaimService.Application.Common.Errors;
using ClaimService.Application.Dtos;
using ClaimService.Application.Interfaces;
using ClaimService.Domain.Entities;
using ClaimService.Domain.Enums;
using ClaimService.Infrastructure.Helpers;
using ClaimService.Infrastructure.Persistence;
using ClaimService.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;


namespace ClaimServiceTest
{
    public class ClaimServiceTests
    {
        private static AppDbContext NewDb()
        {

            var opts = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .EnableSensitiveDataLogging()
        .Options;

            return new TestAppDbContext(opts);
        }

        private static ClaimsPrincipal MakeUser(params string[] roles)
        {
            var claims = roles.Select(r => new System.Security.Claims.Claim(ClaimTypes.Role, r)).ToList();
            var id = new ClaimsIdentity(claims, "TestAuth");
            return new ClaimsPrincipal(id);
        }

        private static ClaimService.Infrastructure.Services.ClaimService NewService(
       AppDbContext db,
       out Mock<IIdGenerator> ids,
       out Mock<IEmployeeServiceClient> emp)
        {
            ids = new Mock<IIdGenerator>();
            ids.Setup(x => x.NextClaimIdAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync("CLM-0002");
            ids.Setup(x => x.NextQueueNoAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(100);

            emp = new Mock<IEmployeeServiceClient>();
            // not needed for most tests here, but available if you test SearchAsync

            return new ClaimService.Infrastructure.Services.ClaimService(db, ids.Object, emp.Object);
        }


        [Fact]
        public async Task CreateDraftAsync_creates_claim_and_audit_event()
        {
            using var db = NewDb();
            var svc = NewService(db, out var ids, out var emp);

            var dto = new CreateClaimDto(
                EmployeeId: "EMP-2",
                ClaimType: 0,
                ClaimDateUtc: DateTime.UtcNow.Date,
                AmountClaimed: 5000m,
                HospitalCode: "H002"
               
            );

            var claim = await svc.CreateDraftAsync(dto, actorUserId: "user-1", CancellationToken.None);

            claim.ClaimId.Should().Be("CLM-0002");
            claim.Status.Should().Be(ClaimStatus.Draft);
           // claim.RowVersion = new byte[] { 1 };
            var events = await db.ClaimEvents.Where(e => e.ClaimId == claim.ClaimId).ToListAsync();
            events.Should().HaveCount(1);
            events[0].FromStatus.Should().Be(ClaimStatus.Draft);
            events[0].ToStatus.Should().Be(ClaimStatus.Draft);
        }

        [Fact]
        public async Task UpdateDraftAsync_without_rowversion_throws_ValidationException()
        {
            using var db = NewDb();
            var svc = NewService(db, out _, out _);

            // Seed a draft claim (ensure RowVersion exists for the stored entity)
            var draft = new ClaimService.Domain.Entities.Claim
            {
                ClaimId = "CLM-0002",
                EmployeeId = "EMP-2",
                ClaimType = 0,
                ClaimDateUtc = DateTime.UtcNow.Date,
                AmountClaimed = 1000m,
                Status = ClaimStatus.Draft,
                CreatedByUserId = "maker",
                CreatedAtUtc = DateTime.UtcNow,
                LastUpdatedAtUtc = DateTime.UtcNow,
              
            };
            db.Claims.Add(draft);
            await db.SaveChangesAsync();

            var dto = new UpdateClaimDto(
                AmountClaimed: 1200m,
                AmountApproved: null,
                HospitalCode: "H002",
                RowVersion: null // <— this triggers the validation error
            );

            var act = () => svc.UpdateDraftAsync("CLM-0002", dto, actorUserId: "maker", CancellationToken.None);

            var ex = await act.Should().ThrowAsync<ValidationException>();
            ex.Which.Errors.Should().ContainKey("rowVersion");
            ex.Which.Errors["rowVersion"].Single().Should().Contain("RowVersion is required");
        }


        // -------------------- TEST 3 --------------------
        [Fact]
        public async Task GenericTransition_invalid_transition_throws_AppException_InvalidTransition()
        {
            using var db = NewDb();
            var svc = NewService(db, out _, out _);

            // Seed a draft claim with a non-null RowVersion so transition pass pre-check
            var draft = new ClaimService.Domain.Entities.Claim
            {
                ClaimId = "CLM-0003",
                EmployeeId = "EMP-3",
                ClaimType = 0,
                ClaimDateUtc = DateTime.UtcNow.Date,
                AmountClaimed = 2000m,
                Status = ClaimStatus.Draft,
                RowVersion = new byte[] { 1 }, // ensure not null
                CreatedByUserId = "maker",
                CreatedAtUtc = DateTime.UtcNow,
                LastUpdatedAtUtc = DateTime.UtcNow,
                 
            };
            db.Claims.Add(draft);
            await db.SaveChangesAsync();

            // user has some role, but transition itself is invalid (Draft -> Approved not allowed)
            var user = MakeUser(PolicyConstants.MedicalWrite);

            var act = () => svc.GenericTransitionAsync(
                claimId: "CLM-0003",
                to: ClaimStatus.Approved,      // invalid from Draft
                remarks: null,
                amountApproved: null,
                actorUserId: "maker",
                user: user,
                ct: CancellationToken.None);

            var ex = await act.Should().ThrowAsync<AppException>();
            ex.Which.ErrorCode.Should().Be("InvalidTransition");
        }

        // -------------------- TEST 4 --------------------
        [Fact]
        public async Task GenericTransition_approve_without_amount_throws_ValidationException()
        {
            using var db = NewDb();
            var svc = NewService(db, out _, out _);

            // Seed a claim already in UnderSMBReview with a non-null RowVersion
            var c = new ClaimService.Domain.Entities.Claim
            {
                ClaimId = "CLM-0004",
                EmployeeId = "EMP-4",
                ClaimType = 0,
                ClaimDateUtc = DateTime.UtcNow.Date,
                AmountClaimed = 5000m,
                Status = ClaimStatus.UnderSMBReview,
                RowVersion = new byte[] { 1 },
                CreatedByUserId = "maker",
                CreatedAtUtc = DateTime.UtcNow,
                LastUpdatedAtUtc = DateTime.UtcNow
            };
            db.Claims.Add(c);
            await db.SaveChangesAsync();

            // User with an allowed role (ANY-of check in your rules)
            var user = MakeUser(PolicyConstants.SmbDecide);

            var act = () => svc.GenericTransitionAsync(
                claimId: "CLM-0004",
                to: ClaimStatus.Approved,
                remarks: "approve",
                amountApproved: null,           // <-- missing -> should fail validation
                actorUserId: "approver",
                user: user,
                ct: CancellationToken.None);

            await act.Should().ThrowAsync<ValidationException>()
                .Where(v => v.Errors.ContainsKey("amountApproved"));
        }
    }
}
