using Civic.API.Models;
using Civic.API.Services.Bills;

namespace Civic.ApiTests.Fakes;

/// <summary>Deterministic <see cref="IBillSource"/> for driving the ingestion tick in tests.</summary>
public class InMemoryBillSource : IBillSource
{
    public List<Bill> Bills { get; set; } = new();

    public Task<IReadOnlyList<Bill>> FetchRecentAsync(int congress, int limit, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Bill>>(Bills.Take(limit).ToList());
}
