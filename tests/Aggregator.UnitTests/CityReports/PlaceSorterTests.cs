using System.Globalization;
using Aggregator.Core.CityReports;
using Aggregator.Core.Model;

namespace Aggregator.UnitTests.CityReports;

[Requirement("R5")]
public sealed class PlaceSorterTests
{
    private static readonly PlaceReport Athens = Report("Athens", "GR", "Attica", distanceKm: 1.4, temperature: 18);
    private static readonly PlaceReport Airport = Report("Athens International Airport", "GR", "Attica", distanceKm: 19.6, temperature: null);
    private static readonly PlaceReport Chania = Report("Chania", "GR", "Crete", distanceKm: 274.9, temperature: 21);

    private static readonly PlaceReport[] Unordered = [Chania, Airport, Athens];

    [Fact]
    public void Sort_Distance_IsNearestFirst() =>
        Assert.Equal([Athens, Airport, Chania], PlaceSorter.Sort(Unordered, new PlaceSort(PlaceSortField.Distance, false)));

    [Fact]
    public void Sort_DistanceDescending_IsFarthestFirst() =>
        Assert.Equal([Chania, Airport, Athens], PlaceSorter.Sort(Unordered, new PlaceSort(PlaceSortField.Distance, true)));

    [Fact]
    public void Sort_Relevance_IsBestMatchRankFirst_AndTiesKeepTheIncomingOrder()
    {
        var village = Report("Chania", "GR", "Peloponnese", 84, null, matchRank: 5);
        var city = Report("Chania", "GR", "Crete", 275, null, matchRank: 2);
        var otherVillage = Report("Chania", "GR", "Central Greece", 56, null, matchRank: 5);
        var prefixOnly = Report("Chania Vasilakiou", "GR", "Peloponnese", 161, null, matchRank: 15);

        var sorted = PlaceSorter.Sort([prefixOnly, village, city, otherVillage], PlaceSort.Default);

        Assert.Equal([city, village, otherVillage, prefixOnly], sorted);
    }

    [Fact]
    public void Sort_Name_IsAlphabeticalIgnoringCase()
    {
        var lowerCase = Report("athens", "GR", "Attica", 50, 10);

        var sorted = PlaceSorter.Sort([Chania, lowerCase, Airport], new PlaceSort(PlaceSortField.Name, false));

        Assert.Equal(["athens", "Athens International Airport", "Chania"], sorted.Select(report => report.Place.Name));
    }

    [Fact]
    public void Sort_NameTies_AreOrderedByCountryThenRegion()
    {
        var inCrete = Report("Agia Marina", "GR", "Crete", 300, null);
        var inAttica = Report("Agia Marina", "GR", "Attica", 30, null);
        var inCyprus = Report("Agia Marina", "CY", "Nicosia", 900, null);

        var sorted = PlaceSorter.Sort([inCrete, inAttica, inCyprus], new PlaceSort(PlaceSortField.Name, false));

        Assert.Equal([inCyprus, inAttica, inCrete], sorted);
    }

    [Fact]
    public void Sort_Name_DoesNotDependOnTheCurrentCulture()
    {
        var izmir = Report("Izmir", "TR", "Izmir", 300, null);
        var istanbul = Report("istanbul", "TR", "Istanbul", 500, null);
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            var sorted = PlaceSorter.Sort([izmir, istanbul], new PlaceSort(PlaceSortField.Name, false));

            Assert.Equal([istanbul, izmir], sorted);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Sort_NameDescending_IsReverseAlphabetical() =>
        Assert.Equal([Chania, Airport, Athens], PlaceSorter.Sort(Unordered, new PlaceSort(PlaceSortField.Name, true)));

    [Fact]
    public void Sort_TemperatureAscending_PutsMissingTemperaturesLast() =>
        Assert.Equal([Athens, Chania, Airport], PlaceSorter.Sort(Unordered, new PlaceSort(PlaceSortField.Temperature, false)));

    [Fact]
    public void Sort_TemperatureDescending_PutsMissingTemperaturesLast() =>
        Assert.Equal([Chania, Athens, Airport], PlaceSorter.Sort(Unordered, new PlaceSort(PlaceSortField.Temperature, true)));

    [Fact]
    public void Sort_TemperatureTies_AreOrderedByDistance()
    {
        var near = Report("Near", "GR", "Attica", 5, 20);
        var far = Report("Far", "GR", "Crete", 250, 20);

        Assert.Equal([near, far], PlaceSorter.Sort([far, near], new PlaceSort(PlaceSortField.Temperature, true)));
    }

    [Fact]
    public void Sort_Empty_ReturnsEmpty() =>
        Assert.Empty(PlaceSorter.Sort([], PlaceSort.Default));

    private static PlaceReport Report(string name, string country, string region, double distanceKm, double? temperature, int matchRank = 0) =>
        new(
            new Place(name, country, region, new Coordinates(0, 0), "PPL"),
            distanceKm,
            matchRank,
            new SourceState(SourceStatus.Ok, null),
            temperature is null ? SourceResult.Unavailable<Weather>() : SourceResult.Ok(new Weather(new CurrentWeather(temperature.Value, 5), [])),
            null,
            null,
            null,
            null);
}
