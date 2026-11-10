// NOTE:
//   Loads the frozen balance of the tests (tests/unit/balance.fixture.json) once before
//   the first unit test runs: the tests pin the formulas, so retuning data/balance.json
//   never breaks them. BalanceTests checks that the real file of the game still loads.
//   Requires: the tests run from inside the repository.
//
//   dotnet test tests/unit/CellOut.Tests.csproj

using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace CellOut.Tests;

internal static class TestSetup
{
    public const string GAME_FILE = "data/balance.json";
    public const string FIXTURE_FILE = "tests/unit/balance.fixture.json";

    /// <summary>Content of a file of the repository, found by walking up from the test binaries.</summary>
    /// <param name="file">(Required) Path relative to the repository root, GAME_FILE or FIXTURE_FILE.</param>
    public static string ReadRepoFile(string file)
    {
        for (DirectoryInfo folder = new(AppContext.BaseDirectory); null != folder; folder = folder.Parent)
        {
            string path = Path.Combine(folder.FullName, file);
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }
        throw new FileNotFoundException($"The tests must run inside the repository, no '{file}' above '{AppContext.BaseDirectory}'.");
    }

    [ModuleInitializer]
    internal static void LoadBalance()
    {
        Balance.Use(ReadRepoFile(FIXTURE_FILE));
    }
}
