using GharAagan.Data;
using GharAagan.Dtos;
using GharAagan.Models;
using GharAagan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GharAagan.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class KycController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    private const long MaxBytes = 5 * 1024 * 1024; // 5 MB
    private static readonly string[] AllowedTypes =
        { "image/jpeg", "image/png", "image/webp", "application/pdf" };

    public KycController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // Documents are stored OUTSIDE wwwroot so they are not publicly served;
    // access goes through the authorized File() endpoint below.
    private string StoreRoot => Path.Combine(_env.ContentRootPath, "kyc-store");

    // Provider: my current KYC status + documents.
    [Authorize(Roles = "Provider")]
    [HttpGet("me")]
    public async Task<ActionResult<KycStatusResponse>> Me()
    {
        var id = User.GetUserId();
        var user = await _db.Users.Include(u => u.KycDocuments).FirstAsync(u => u.Id == id);
        return Ok(ToStatus(user));
    }

    // Provider: submit (or resubmit) KYC documents. Replaces any previous submission.
    [Authorize(Roles = "Provider")]
    [HttpPost("submit")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<ActionResult<KycStatusResponse>> Submit([FromForm] KycSubmitForm form)
    {
        var id = User.GetUserId();
        var user = await _db.Users.Include(u => u.KycDocuments).FirstAsync(u => u.Id == id);

        var incoming = new (IFormFile? file, KycDocType type)[]
        {
            (form.NationalId, KycDocType.NationalId),
            (form.Passport, KycDocType.Passport),
            (form.DrivingLicense, KycDocType.DrivingLicense),
            (form.Selfie, KycDocType.Selfie),
            (form.AddressProof, KycDocType.AddressProof),
        }.Where(x => x.file is { Length: > 0 }).ToList();

        if (incoming.Count == 0)
            return BadRequest("Upload at least one document.");

        foreach (var (file, _) in incoming)
        {
            if (file!.Length > MaxBytes)
                return BadRequest($"{file.FileName} exceeds the 5 MB limit.");
            if (!AllowedTypes.Contains(file.ContentType))
                return BadRequest($"{file.FileName}: only JPG, PNG, WEBP or PDF are allowed.");
        }

        // Resubmission: remove old DB rows + files.
        var providerDir = Path.Combine(StoreRoot, id.ToString());
        if (user.KycDocuments.Count > 0)
        {
            foreach (var old in user.KycDocuments)
            {
                var path = Path.Combine(providerDir, old.StoredName);
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
            _db.KycDocuments.RemoveRange(user.KycDocuments);
        }

        Directory.CreateDirectory(providerDir);
        foreach (var (file, type) in incoming)
        {
            var ext = Path.GetExtension(file!.FileName);
            var stored = $"{Guid.NewGuid():N}{ext}";
            await using (var fs = System.IO.File.Create(Path.Combine(providerDir, stored)))
                await file.CopyToAsync(fs);

            _db.KycDocuments.Add(new KycDocument
            {
                ProviderId = id,
                DocType = type,
                StoredName = stored,
                FileName = Path.GetFileName(file.FileName),
                ContentType = file.ContentType
            });
        }

        user.KycStatus = KycStatus.Pending;
        user.KycSubmittedAt = DateTime.UtcNow;
        user.KycReviewedAt = null;
        user.KycRejectionReason = null;
        await _db.SaveChangesAsync();

        await _db.Entry(user).Collection(u => u.KycDocuments).LoadAsync();
        return Ok(ToStatus(user));
    }

    // Stream a document. Allowed only for an admin or the owning provider.
    [HttpGet("file/{docId:int}")]
    public async Task<IActionResult> File(int docId)
    {
        var doc = await _db.KycDocuments.FirstOrDefaultAsync(d => d.Id == docId);
        if (doc is null) return NotFound();
        if (User.GetRole() != "Admin" && doc.ProviderId != User.GetUserId())
            return Forbid();

        var path = Path.Combine(StoreRoot, doc.ProviderId.ToString(), doc.StoredName);
        if (!System.IO.File.Exists(path)) return NotFound();

        var stream = System.IO.File.OpenRead(path);
        return File(stream, doc.ContentType ?? "application/octet-stream");
    }

    private KycStatusResponse ToStatus(User u) => new()
    {
        Status = u.KycStatus.ToString(),
        IsVerified = u.IsVerified,
        SubmittedAt = u.KycSubmittedAt,
        ReviewedAt = u.KycReviewedAt,
        RejectionReason = u.KycRejectionReason,
        Documents = u.KycDocuments
            .OrderBy(d => d.DocType)
            .Select(d => new KycDocItem
            {
                Id = d.Id,
                DocType = d.DocType.ToString(),
                FileName = d.FileName,
                Url = $"/api/kyc/file/{d.Id}",
                UploadedAt = d.UploadedAt
            }).ToList()
    };
}

public class KycSubmitForm
{
    public IFormFile? NationalId { get; set; }
    public IFormFile? Passport { get; set; }
    public IFormFile? DrivingLicense { get; set; }
    public IFormFile? Selfie { get; set; }
    public IFormFile? AddressProof { get; set; }
}
