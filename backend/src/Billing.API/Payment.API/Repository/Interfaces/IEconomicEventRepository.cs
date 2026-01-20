using Payment.API.Models;

namespace Payment.API.Repository.Interfaces;

public interface IEconomicEventRepository
{
    Task InsertManyAsync(IEnumerable<EconomicEventDocument> events, CancellationToken cancellationToken);
}

