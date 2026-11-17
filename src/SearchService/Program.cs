using Meilisearch;
using SearchService.Data;
using SearchService.Endpoints;
using SearchService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton(sp => {
    var configuration = sp.GetRequiredService<IConfiguration>();

    return new MeilisearchClient(
        configuration["Meilisearch:Url"],
        configuration["Meilisearch:ApiKey"]
        );
});
builder.Services.AddHttpClient<AuctionSvcHttpClient>()
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);
        options.Retry.MaxRetryAttempts = 5;
        options.Retry.Delay = TimeSpan.FromSeconds(10);
        options.Retry.OnRetry = args =>
        {
            Console.WriteLine(
                $"Auction svc unavailable. Retry {args.AttemptNumber + 1}/5");
            return default;
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
try
{
    await DbInitializer.ConfigureIndex(app);
}
catch (Exception e)
{
    Console.WriteLine($"Failed to seed search: {e.Message}");
}

app.MapGet("/api/search", SearchEndpoints.GetSearchResult);
app.MapGet("/api/search/{id}", SearchEndpoints.GetAuctionById);

_ = Task.Run(async () =>
{
    try
    {
        await DbInitializer.FetchMissingAuctions(app);
    }
    catch (Exception e)
    {
        Console.WriteLine($"Failed to seed search: {e.Message}");
    }
});

app.Run();

