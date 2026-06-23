using System.ComponentModel.DataAnnotations;

namespace GharAagan.Models;

public class KycDocument
{
    public int Id { get; set; }

    public int ProviderId { get; set; }
    public User? Provider { get; set; }

    public KycDocType DocType { get; set; }

    // Stored filename on disk (under the content-root kyc store, NOT wwwroot — see KycController).
    [Required, MaxLength(260)]
    public string StoredName { get; set; } = string.Empty;

    // Original uploaded filename, for display.
    [MaxLength(260)]
    public string? FileName { get; set; }

    [MaxLength(100)]
    public string? ContentType { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
