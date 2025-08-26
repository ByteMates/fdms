namespace ClaimService.Domain.Enums
{
    public enum ClaimStatus
    {
        Draft = 0,
        Submitted = 1,
        UnderHospitalReview = 2,
        UnderSMBReview = 3,
        Approved = 4,
        Rejected = 5,
        Returned = 6
    }
}
