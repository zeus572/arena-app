namespace Civic.API.Services;

public interface ISeedService
{
    Task SeedAsync(CancellationToken ct = default);
}
