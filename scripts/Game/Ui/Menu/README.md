# Game/Ui/Menu

The pieces of the title menu (Game/Ui/TitleMenu.cs coordinates them): the Milky Way to look around in, the star systems in it and one planet up close with its start phases.

| File | Content |
| --- | --- |
| GalaxyView.cs | The galaxy from inside (shaders/galaxy.gdshader, Sgr A* in the middle): drag pans, wheel zooms, the playable planets are buttons on their orbits; flies onto a planet and back. |
| GalaxySystemsLayer.cs | Draws every star system of Core/Galaxy.cs: star by kind with its glow, orbits, planets as dots; fades in when close. |
| PlanetView.cs | One planet up close: big globe in its star system, name, text, traits, the start phases (open by Core/PlanetProgress.cs, F10 opens all), start and back. |
| StarSystemView.cs | The star system behind the planet view: the star at the left edge, flat orbits, the neighbors (Core/StarSystem.cs) travelling on them. |
| PlanetGlobe.cs | One planet as a turning globe (shaders/planet_globe.gdshader): continents, ice, clouds, gas giant bands, atmosphere. |
