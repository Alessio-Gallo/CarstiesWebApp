using Contracts;
using Mapster;
using Meilisearch;
using SearchService.Models;

namespace SearchService.Handlers;

public class AuctionUpdatedHandler
{
    public async Task Handle(AuctionUpdated message, MeilisearchClient client)
    {
        await client.Index("items").UpdateDocumentsAsync([message]);
    }
}