using GharAagan.Data;
using Microsoft.EntityFrameworkCore;

namespace GharAagan.Services;

/// <summary>
/// Rule-based provider ranking (the "AI recommendation" of the research proposal).
///
/// Weighted score, explained for viva/demo:
///
///     Score = (AverageRating / 5) * 0.60   // service quality signal (reviews)
///           +  LocationMatch     * 0.25    // proximity signal (same area = faster service)
///           +  VerifiedBonus     * 0.15    // trust signal (admin-verified identity)
///
/// - AverageRating is normalized to 0..1 by dividing by the max rating (5),
///   so all three components share the same 0..1 scale before weighting.
/// - LocationMatch is 1.0 when the customer's location text overlaps the
///   provider's service area (case-insensitive token match), else 0.0.
/// - VerifiedBonus is 1.0 for admin-verified providers, else 0.0.
///
/// The weights (0.6 / 0.25 / 0.15) prioritize proven quality first, then
/// proximity, then verification — and can be tuned without code changes
/// elsewhere because callers only see the final ranked list.
/// </summary>
public class RecommendationService : IRecommendationService
{
    private const double RatingWeight = 0.60;
    private const double LocationWeight = 0.25;
    private const double VerifiedWeight = 0.15;

    private readonly ApplicationDbContext _context;

    public RecommendationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<RankedProvider>> GetRankedProvidersAsync(int categoryId, string? customerLocation)
    {
        var providers = await _context.ProviderProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.ServiceCategory)
            .Where(p => p.ServiceCategoryId == categoryId)
            .ToListAsync();

        var ranked = providers
            .Select(p =>
            {
                double ratingComponent = (p.AverageRating / 5.0) * RatingWeight;
                double locationComponent = LocationMatches(customerLocation, p.Location) ? LocationWeight : 0.0;
                double verifiedComponent = p.IsVerified ? VerifiedWeight : 0.0;

                return new RankedProvider(p, Math.Round(ratingComponent + locationComponent + verifiedComponent, 4));
            })
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Provider.YearsExperience)
            .ToList();

        return ranked;
    }

    /// <summary>
    /// Case-insensitive token overlap between customer location and provider
    /// service area. "Baneshwor, Kathmandu" matches "Kathmandu - Baneshwor".
    /// </summary>
    private static bool LocationMatches(string? customerLocation, string providerLocation)
    {
        if (string.IsNullOrWhiteSpace(customerLocation)) return false;

        var separators = new[] { ' ', ',', '-', '/' };
        var customerTokens = customerLocation
            .Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .ToHashSet();

        return providerLocation
            .Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(t => customerTokens.Contains(t.ToLowerInvariant()));
    }
}
