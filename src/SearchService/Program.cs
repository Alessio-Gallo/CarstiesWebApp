using Mapster;
using Meilisearch;
using SearchService.Data;
using SearchService.Endpoints;
using SearchService.Services;
using Wolverine;
using Wolverine.RabbitMQ;

TypeAdapterConfig.GlobalSettings.Scan(typeof(Program).Assembly);
    
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

builder.Host.UseWolverine(opts =>
{
    opts.UseRabbitMq(rabbit =>
        {
            rabbit.HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
            rabbit.UserName = builder.Configuration["RabbitMQ:Username"] ?? "guest";
            rabbit.Password = builder.Configuration["RabbitMQ:Password"] ?? "guest";
        })
        .DeclareExchange("auction-created", ex => ex.ExchangeType = ExchangeType.Fanout)
        .DeclareExchange("auction-updated", ex => ex.ExchangeType = ExchangeType.Fanout)
        .DeclareExchange("auction-deleted", ex => ex.ExchangeType = ExchangeType.Fanout)
        .BindExchange("auction-created").ToQueue("search-auction-created")
        .BindExchange("auction-updated").ToQueue("search-auction-updated")
        .BindExchange("auction-deleted").ToQueue("search-auction-deleted")
        .AutoProvision();
    
    opts.ListenToRabbitQueue("search-auction-created");
    opts.ListenToRabbitQueue("search-auction-updated");
    opts.ListenToRabbitQueue("search-auction-deleted");
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

