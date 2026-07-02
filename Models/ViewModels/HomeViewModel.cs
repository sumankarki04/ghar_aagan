namespace GharAagan.Models.ViewModels;

/// <summary>Marketplace home page: hero search, categories, featured providers, trust stats.</summary>
public class HomeViewModel
{
    public List<ServiceCategory> Categories { get; set; } = new();
    public List<ProviderProfile> FeaturedProviders { get; set; } = new();
    public int VerifiedProviderCount { get; set; }
    public int CompletedJobCount { get; set; }
    public double AverageRating { get; set; }
}
