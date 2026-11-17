# Civ

The civilization stage (stage 8, the antique age): the tribe founds its city at the fire, the headquarters stands there, the city gathers knowledge, researches techs and sails to the other islands. Society (Tribe/Society.cs) owns and wires it.

| File | Content |
| --- | --- |
| CivKeeper.cs | The city: founding at the fire (the hall on the old fire), knowledge per minute, research, save. |
| TechTree.cs | Pure logic: the nine techs in three tiers, what they need and cost, the bonuses they add up to. |
| TechTreeView.cs | The tech tree as a full screen view: glass tiles, drawn symbols, glowing lines, T opens it. |
| ShipKeeper.cs | With Sailing the chieftain becomes a ship on the sea, the retinue goes on board and ashore. |
| ShipModel.cs | The galley as one node: model in a color, on the waterline, rocking. Used by the chieftain and by landing neighbors. |
| CityScenery.cs | What the techs change in the world: fields around the city (Farming), a pier at the nearest shore (Sailing). |
