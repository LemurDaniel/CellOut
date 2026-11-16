// NOTE:
//   Tests of the sideways bend and the cross-section per vertebra
//   (scripts/Creature/Design/CreatureSpine.cs, CreatureDesign).
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter SpineShapeTests

using Godot;
using Xunit;

namespace CellOut.Tests;

public class SpineShapeTests
{
    private static CreatureDesign Body()
    {
        return CreatureDesign.FromGirths(CreatureType.Fish, new[] { 0.4f, 0.6f, 0.8f, 0.8f, 0.6f, 0.5f }, null);
    }

    [Fact]
    public void SwayCurlsTheBodyAndTheNeighborsFollow()
    {
        CreatureDesign body = Body();
        body.SetSway(0, 0.9f);

        Assert.Equal(0.9f, body.Sways[0], 4);
        Assert.True(body.Sways[1] > 0.5f, "the neighbor follows within the step limit");
        Assert.Equal(0.9f, body.SwayAt(body.TailTipX), 3);                     // the cap keeps the offset of its end
        Assert.Equal(body.SwayAt(body.NoseTipX), body.CenterAt(body.NoseTipX).Z, 4);

        // The shoulder and the inside test follow the curled spine
        float x = body.VertebraX(0);
        Assert.True(body.Contains(new Vector3(x, 0f, 0.9f)));
        Assert.False(body.Contains(new Vector3(x, 0f, 0f)), "the middle of the plane lies outside the tail now");
        Assert.True(body.TryPlace(CreaturePartKind.Limb, 0.1f, 0));
        Assert.True(body.LimbRoot(body.Parts[^1]).Z > body.SwayAt(body.XAt(0.1f)));
    }

    [Fact]
    public void FlatnessPerVertebraShapesTheRing()
    {
        CreatureDesign body = Body();
        float noseX = body.VertebraX(5);
        float tailX = body.VertebraX(0);
        float top   = body.TopAt(noseX);
        float flank = body.FlankAt(noseX);

        body.SetFlat(5, 1f);

        Assert.True(body.TopAt(noseX) < top * 0.6f, "a flat head is lower");
        Assert.True(body.FlankAt(noseX) > flank * 1.2f, "and wider");
        Assert.Equal(0f, body.FlatnessAt(tailX), 3);                           // the tail keeps its round ring

        // The value of the whole body adds up with the vertebra, clamped
        body.SetFlatness(1f);
        Assert.Equal(1f, body.FlatnessAt(noseX), 4);
        body.SetFlat(5, -9f);
        Assert.Equal(-1f, body.Flats[5], 4);
    }

    [Fact]
    public void NewVertebraeContinueTheShape()
    {
        CreatureDesign body = Body();
        body.SetSway(5, 0.3f);
        body.SetFlat(5, 0.5f);

        Assert.True(body.GrowEnd(isNose: true));

        Assert.Equal(body.Girths.Count, body.Sways.Count);
        Assert.Equal(body.Girths.Count, body.Flats.Count);
        Assert.Equal(0.5f, body.Flats[^1], 4);
        Assert.True(body.Sways[^1] >= 0.3f, "the new nose continues the curve");
    }
}