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
    public void EveryVertebraScalesPerAxis()
    {
        CreatureDesign body = Body();
        float noseX = body.VertebraX(5);
        float tailX = body.VertebraX(0);
        float top   = body.TopAt(noseX);
        float flank = body.FlankAt(noseX);

        float tailTop = body.TopAt(tailX);

        // Y and Z: height and width of the ring, each on its own
        body.SetHeight(5, 0.5f);
        Assert.Equal(top * 0.5f, body.TopAt(noseX), 3);
        Assert.Equal(flank, body.FlankAt(noseX), 3);                           // the width stays
        body.SetWidth(5, 1.5f);
        Assert.Equal(flank * 1.5f, body.FlankAt(noseX), 3);
        Assert.Equal(tailTop, body.TopAt(tailX), 3);                           // the tail keeps its ring

        // Clamped
        body.SetHeight(5, 0f);
        Assert.Equal(CreatureSpine.MIN_CROSS, body.Heights[5], 4);
        body.SetWidth(5, 99f);
        Assert.Equal(CreatureSpine.MAX_CROSS, body.Widths[5], 4);

        // X: both distances to the neighbors grow, the vertebra itself stays
        float x    = body.Positions[2];
        float gap  = body.Positions[3] - body.Positions[2];
        float span = body.Positions[^1] - body.Positions[0];
        body.StretchVertebra(2, 1.5f);
        Assert.Equal(x, body.Positions[2], 4);
        Assert.Equal(gap * 1.5f, body.Positions[3] - body.Positions[2], 3);
        Assert.True(body.Positions[^1] - body.Positions[0] > span, "the body got longer");
        body.StretchVertebra(2, 99f);
        Assert.Equal(CreatureSpine.MAX_GAP, body.Positions[3] - body.Positions[2], 3);
    }

    [Fact]
    public void NewVertebraeContinueTheShape()
    {
        CreatureDesign body = Body();
        body.SetSway(5, 0.3f);
        body.SetHeight(5, 0.5f);

        Assert.True(body.GrowEnd(isNose: true));

        Assert.Equal(body.Girths.Count, body.Sways.Count);
        Assert.Equal(body.Girths.Count, body.Heights.Count);
        Assert.Equal(body.Girths.Count, body.Widths.Count);
        Assert.Equal(0.5f, body.Heights[^1], 4);
        Assert.True(body.Sways[^1] >= 0.3f, "the new nose continues the curve");
    }

    [Fact]
    public void RingShapeAndLimitsOfALongThinBody()
    {
        // Keel, round or boxy per half of a ring, clamped, carried on to new vertebrae
        CreatureDesign body = Body();
        body.SetCrown(5, -9f);
        body.SetBelly(5, 0.7f);
        Assert.Equal(-1f, body.Crowns[5], 4);
        Assert.Equal(-1f, body.CrownAt(body.VertebraX(5)), 3);
        Assert.Equal(0f, body.CrownAt(body.VertebraX(0)), 3);
        Assert.True(body.GrowEnd(isNose: true));
        Assert.Equal(body.Girths.Count, body.Crowns.Count);
        Assert.Equal(body.Girths.Count, body.Bellies.Count);
        Assert.Equal(0.7f, body.Bellies[^1], 4);

        // A centipede: up to MAX_VERTEBRAE vertebrae, a waist down to MIN_GIRTH
        while (body.GrowEnd(isNose: false)) { }
        Assert.Equal(CreatureDesign.MAX_VERTEBRAE, body.Girths.Count);
        Assert.True(CreatureDesign.MAX_VERTEBRAE >= 24);
        body.SetGirth(3, 0f);
        Assert.Equal(CreatureDesign.MIN_GIRTH, body.Girths[3], 4);
        Assert.True(CreatureDesign.MIN_GIRTH <= 0.1f);
    }
}