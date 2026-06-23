namespace GharAagan.Models;

public enum UserRole
{
    Customer = 0,
    Provider = 1,
    Admin = 2
}

public enum BookingStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Completed = 3,
    Cancelled = 4
}

public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2
}

public enum KycStatus
{
    NotSubmitted = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum KycDocType
{
    NationalId = 0,
    Passport = 1,
    DrivingLicense = 2,
    Selfie = 3,
    AddressProof = 4
}
