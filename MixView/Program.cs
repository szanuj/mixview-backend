using Microsoft.AspNetCore.Http.HttpResults;
using Newtonsoft.Json;
using MixView;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

const string API_URL = "https://api.carbonintensity.org.uk/generation";
List<Mix> data = [];
string[] clean =
[
    "biomass",
    "nuclear",
    "hydro",
    "wind",
    "solar",
];

app.MapGet("/", async () =>
{
    DateTime today = DateTime.UtcNow.Date;
    DateTime start = today.AddMinutes(1);
    DateTime end = today.AddDays(3);

    data = await FetchData(start, end) ?? [];

    var dayGroups = data.GroupBy(i => i.From.Day).ToList();

    List<DayData> days = [];
    foreach (var group in dayGroups)
    {
        List<Mix> items = [.. group];
        days.Add(new DayData() { Summary=GetSummary(items), Data=items});
    }

    return days;
});

app.MapGet("/charge/{duration}", Results<Ok<Span>, NotFound> (int duration) =>
{
    throw new NotImplementedException();
});

app.Run();



static async Task<List<Mix>?> FetchData(DateTime from, DateTime to)
{
    HttpClient client = new();
    string uri = $"{API_URL}/{from:s}/{to:s}";
    var response = await client.GetAsync(uri);
    string content = await response.Content.ReadAsStringAsync();
    GenerationApiResponse? parsed = JsonConvert.DeserializeObject<GenerationApiResponse>(content);
    return parsed?.Data;
}

MixSummary? GetSummary(List<Mix> data)
{
    if (data.Count == 0)
        return null;

    IEnumerable<string> fuels = data.First().Generationmix.Select(item => item.Fuel);
    List<MixMember> mean = [];

    foreach (string fuel in fuels)
        mean.Add(new(fuel, data.Select(i => i.Generationmix.Find(j => j.Fuel == fuel)).Average(i => i.Perc)));

    double cleanPerc = mean.Where(i => clean.Contains(i.Fuel)).Sum(i => i.Perc);

    return new(mean, cleanPerc);
}

namespace MixView
{
    public record Span(DateTime From, DateTime To);
    public record MixMember(string Fuel, double Perc);
    public record Mix(DateTime From, DateTime To, List<MixMember> Generationmix);
    public record MixSummary(List<MixMember> Mean, double Cleanperc);
    public class DayData
    {
        public MixSummary? Summary { get; set; }
        public List<Mix> Data { get; set; } = [];
    }
    public class GenerationApiResponse
    {
        public List<Mix> Data { get; set; } = [];
    }
}
