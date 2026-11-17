using Meilisearch;
using SearchService.Data;
using SearchService.Endpoints;
using SearchService.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton(sp => {
    var configuration = sp.GetRequiredService<IConfiguration>();

    return new MeilisearchClient(
        configuration["Meilisearch:Url"],
        configuration["Meilisearch:ApiKey"]
        );
});

var app = builder.Build();

// Configure the HTTP request pipeline.
try
{
    await DbInitializer.InitDb(app);
}
catch (Exception e)
{
    Console.WriteLine($"Failed to seed search: {e.Message}");
}

app.MapGet("/api/search", SearchEndpoints.GetSearchResult);


app.Run();

