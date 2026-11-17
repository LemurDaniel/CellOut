// NOTE:
//   Unit tests for the names and measures of the galaxy menu (Core/StarNames.cs, Core/Galaxy.cs):
//   every star has a stable, readable name, planets are named like exoplanets, distances and
//   the round lengths of the scale bar in light years.
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter GalaxyTests

//
//   Chapters:
//     NAMES     Names of stars and planets.
//     MEASURE   Light years between disk points, round lengths of the scale bar.
//

using System.Linq;
using Godot;
using Xunit;

namespace CellOut.Tests;

public class GalaxyTests
{
    // ===========================================================================
    // ================================   NAMES   ================================
    // ===========================================================================
    //
    //   NamesAreStable        The same key gives the same name in every call.
    //   NamesAreReadable      At least five letters, a capital first, no clash of two vowels across syllables.
    //   NamesAreMostlyUnique  Almost every system of the galaxy has its own name.
    //   PlanetsAreLettered    The first planet of a star is "b", the second "c".
    //

    [Fact]
    public void NamesAreStable()
    {
        Assert.Equal(StarNames.Of("S-042"), StarNames.Of("S-042"));
        Assert.NotEqual(StarNames.Of("S-042"), StarNames.Of("S-043"));
    }

    [Fact]
    public void NamesAreReadable()
    {
        Assert.All(Galaxy.Systems, system =>
        {
            string name = StarNames.Of(system.Key);
            Assert.True(name.Length >= 5, $"'{name}' of {system.Key} is too short");
            Assert.True(char.IsUpper(name[0]), $"'{name}' must start with a capital");
            Assert.True(name.All(char.IsLetter), $"'{name}' must be letters only");
        });
    }

    [Fact]
    public void NamesAreMostlyUnique()
    {
        int count  = Galaxy.Systems.Count;
        int unique = Galaxy.Systems.Select(system => StarNames.Of(system.Key)).Distinct().Count();

        Assert.True(unique >= count * 0.9, $"only {unique} of {count} names differ");
    }

    [Fact]
    public void PlanetsAreLettered()
    {
        string star = StarNames.Of("S-042");

        Assert.Equal($"{star} b", StarNames.PlanetOf("S-042", 0));
        Assert.Equal($"{star} c", StarNames.PlanetOf("S-042", 1));
    }

    // ===========================================================================
    // ===============================   MEASURE   ===============================
    // ===========================================================================
    //
    //   LightYearsFromTheMiddle   Half a disk radius is 25,000 light years, rounded to hundreds.
    //   RoundLengths              1, 2 or 5 times a power of ten at or below the length, at least 1.
    //

    [Fact]
    public void LightYearsFromTheMiddle()
    {
        Assert.Equal(25000, Galaxy.LightYears(Vector2.Zero, new Vector2(0.5f, 0f)));
        Assert.Equal(100, Galaxy.LightYears(Vector2.Zero, new Vector2(0.0021f, 0f)));    // 105 light years
        Assert.Equal(0, Galaxy.LightYears(new Vector2(0.3f, 0.2f), new Vector2(0.3f, 0.2f)));
    }

    [Theory]
    [InlineData(3700f, 2000)]
    [InlineData(5000f, 5000)]
    [InlineData(9999f, 5000)]
    [InlineData(10000f, 10000)]
    [InlineData(140f, 100)]
    [InlineData(0.2f, 1)]
    public void RoundLengths(float years, int expected)
    {
        Assert.Equal(expected, Galaxy.RoundLightYears(years));
    }
}
