using Microsoft.AspNetCore.Http.HttpResults;
using Newtonsoft.Json;
using MixView;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b =>
    {
        b.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");


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



app.MapOpenApi();

app.MapGet("/", async () =>
{
    await UpdateData();

    var dayGroups = data.GroupBy(i => i.From.Day).ToList();

    List<DayData> days = [];
    foreach (var group in dayGroups)
    {
        List<Mix> items = [.. group];
        days.Add(new DayData() { Summary=GetSummary(items), Data=items});
    }

    return days;
});

app.MapGet("/charge/{duration}", async Task<Results<Ok<ChargeInfo>, BadRequest<string>>> (int duration) =>
{
    try
    {
        return TypedResults.Ok(await CalcChargeSpan(duration));
    }
    catch(Exception e)
    {
        return TypedResults.BadRequest(e.Message);
    }
});

app.Run();


int GetTomorrowIndex()
{
    int result = 0;
    int prev = data[0].From.Day;
    
    for (int i = 0; i < data.Count; i++)
    {
        int curr = data[i].From.Day;
        if (curr != prev)
            return i;
        prev = curr;
    }
    return result;
}


async Task<ChargeInfo?> CalcChargeSpan(int duration)
{
    await UpdateData();

    int tomorrowIndex = GetTomorrowIndex();

    int intervals = 2 * duration;
    if (tomorrowIndex + intervals > data.Count)
        return null;
    
    Queue<Mix> dataRange = new(data.GetRange(0, intervals - 1));
    List<(double, Span)> spans = [];

    for (int i = tomorrowIndex; i + intervals <= data.Count; i++)
    {
        dataRange.Enqueue(data[i + intervals - 1]);

        spans.Add(
        (
            GetSummary(dataRange)?.Cleanperc ?? 0.0,
            new (dataRange.First().From, dataRange.Last().To)
        ));

        dataRange.Dequeue();
    }

    (double, Span) maxItem = spans.MaxBy(s => s.Item1);

    return new ChargeInfo(maxItem.Item2.From, maxItem.Item2.To, maxItem.Item1);
}


async Task UpdateData()
{
    DateTime today = DateTime.UtcNow.Date;
    DateTime start = today.AddMinutes(1);
    DateTime end = today.AddDays(3);

    data = await FetchData(start, end) ?? [];
}


async Task<List<Mix>?> FetchData(DateTime from, DateTime to)
{
    HttpClient client = new();
    string uri = $"{API_URL}/{from:s}/{to:s}";
    var response = await client.GetAsync(uri);
    string content = await response.Content.ReadAsStringAsync();
    GenerationApiResponse? parsed = JsonConvert.DeserializeObject<GenerationApiResponse>(content);
    return parsed?.Data;
}

MixSummary? GetSummary(IEnumerable<Mix> data)
{
    if (data.Count() == 0)
        return null;

    IEnumerable<string> fuels = data.First().Generationmix.Select(item => item.Fuel);
    List<MixMember> mean = [];

    foreach (string fuel in fuels)
        mean.Add(new(fuel, data.Select(i => i.Generationmix.Find(j => j.Fuel == fuel)).Average(i => i?.Perc ?? 0.0)));

    double cleanPerc = mean.Where(i => clean.Contains(i.Fuel)).Sum(i => i.Perc);

    return new(mean, cleanPerc);
}


namespace MixView
{
    public record Span(DateTime From, DateTime To);
    public record ChargeInfo(DateTime From, DateTime To, double Cleanperc);
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
