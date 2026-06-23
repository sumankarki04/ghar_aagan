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
