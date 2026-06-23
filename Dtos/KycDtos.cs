using System.ComponentModel.DataAnnotations;

namespace GharAagan.Dtos;

public class KycDocItem
{
    public int Id { get; set; }
    public string DocType { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string Url { get; set; } = string.Empty;   // authorized stream endpoint
    public DateTime UploadedAt { get; set; }
}

public class KycStatusResponse
{
    public string Status { get; set; } = string.Empty;          // NotSubmitted | Pending | Approved | Rejected
    public bool IsVerified { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    public List<KycDocItem> Documents { get; set; } = new();
}

public class KycReviewItem
{
    public int ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? RejectionReason { get; set; }
    public List<KycDocItem> Documents { get; set; } = new();
}

public class KycRejectRequest
{
    [Required, MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
