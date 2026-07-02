using GharAagan.Models;

namespace GharAagan.Services;

/// <summary>
/// A provider with its computed recommendation score, returned ranked (best first).
/// </summary>
public record RankedProvider(ProviderProfile Provider, double Score);

/// <summary>
/// Ranks providers within a category for a customer.
/// Stands in for the "AI recommendation" module of the research proposal —
/// intentionally simple and rule-based so it can be explained in a viva/demo.
/// </summary>
public interface IRecommendationService
{
    /// <param name="categoryId">Service category to search within.</param>
    /// <param name="customerLocation">
    /// Customer's location text (e.g. "Kathmandu - Baneshwor"); may be null.
    /// </param>
    Task<List<RankedProvider>> GetRankedProvidersAsync(int categoryId, string? customerLocation);
}
