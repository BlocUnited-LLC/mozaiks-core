using MongoDB.Driver;
using Payment.API.Models;
using Payment.API.Repository.Interfaces;

namespace Payment.API.Repository;

public sealed class EconomicEventRepository : IEconomicEventRepository
{
    private readonly IMongoCollection<EconomicEventDocument> _events;

    public EconomicEventRepository(IMongoDatabase db)
    {
        _events = db.GetCollection<EconomicEventDocument>("EconomicEvents");
    }

    public async Task InsertManyAsync(IEnumerable<EconomicEventDocument> events, CancellationToken cancellationToken)
    {
        var list = events.ToList();
        if (list.Count == 0)
        {
            return;
        }

        try
        {
            await _events.InsertManyAsync(list, new InsertManyOptions { IsOrdered = false }, cancellationToken);
        }
        catch (MongoBulkWriteException<EconomicEventDocument> ex) when (ex.WriteErrors.All(e => e.Category == ServerErrorCategory.DuplicateKey))
        {
            // idempotency: ignore duplicates
        }
    }
}

