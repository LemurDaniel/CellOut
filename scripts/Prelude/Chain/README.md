# Prelude/Chain

The rules of the RNA prelude (`Prelude/RnaWorld.cs`) as pure logic without engine calls, made for unit tests.

| File | Content |
| --- | --- |
| `RnaTemplate.cs` | The bases (`RnaBase`), their pairing (A-U, G-C) and the random template that grows on demand: the base wanted at a position. |
| `RnaChain.cs` | The links behind one head: base, mutation trait and plane position, paired and mutation counts, following the head, finding the bitten link and cutting the chain. |
