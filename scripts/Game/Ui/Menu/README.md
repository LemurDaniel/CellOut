# Game/Ui/Menu

The pieces of the title menu (Game/Ui/TitleMenu.cs coordinates them): the Milky Way to look around in, the star systems in it and one planet up close with its start phases.

| File | Content |
| --- | --- |
| GalaxyView.cs | The galaxy from inside (shaders/galaxy.gdshader, Sgr A* in the middle): drag pans, wheel zooms, hovering a star or Sgr A* shows its card, a click flies there (a playable star picks its planet); the playable planets are buttons on their orbits with a ring of the phases reached; flies onto a planet and back with warp streaks. |
| GalaxySystemsLayer.cs | Draws every star system of Core/Galaxy.cs: star by kind with its glow, orbits, planets as dots once close; the hovered system in full with turning brackets. |
| GalaxyHud.cs | Scale bar in light years bottom left, pointers at the screen edge to the playable planets out of sight. |
| SystemCard.cs | The hover card: star name (Core/StarNames.cs), kind, its planets as a row of dots, light years from the current planet, life or "unexplored until the space age"; Sgr A* has its own. |
| PlanetView.cs | One planet up close: big globe with its moons in its star system, name, text, traits, the start phases (open by Core/PlanetProgress.cs, F10 opens all), start (the globe rushes at the camera) and back. |
| StarSystemView.cs | The star system behind the planet view: the camera rides with the planet along its orbit, the star (shaders/menu_sun.gdshader) circles it and lights every globe from its side, wide flat orbits, the neighbors (Core/StarSystem.cs) on screen with rings, named on hover, a belt of rocks in some systems, now and then a comet. |
| PlanetGlobe.cs | One planet as a turning globe (shaders/planet_globe.gdshader): continents, ice, clouds, gas giant bands, rings, atmosphere. |
