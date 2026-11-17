# Game/Water

The water around the player: its floor, what floats in it, its colors and the lens over it. `World/FollowCamera.cs` creates and drives all of them.

| File | Content |
| --- | --- |
| `Backdrop.cs` | Sea floor with caustics, motes at several depths and big soft blobs close to the camera, seeded per grid cell. |
| `SwimWake.cs` | Wake of every swimming cell in the cell stages: counter turning swirls from its flanks and rising bubbles, shed per distance swum. |
| `SoupPalette.cs` | Pure data: the colors of the water per stage (primordial soup, pond, sea) and the blend between them. |
| `SoupLens.cs` | Light shafts and vignette over the 3D view (`shaders/soup_lens.gdshader`), below every 2D layer. |