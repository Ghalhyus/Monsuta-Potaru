using System.Diagnostics;
using System.Text;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Tests JavaScript du parc (<c>MonsutaPotaru.Tests/js/*.test.mjs</c>, sur <c>wwwroot/js/parc-modele.js</c> : cartes, chemins,
/// hasard, caractère, humeur), lancés par Node (<c>node --test</c>) pour que <c>dotnet test</c> les passe aussi, sur le PC
/// comme en CI. La couverture du module est écrite au format LCOV (<see cref="LcovFile"/>), lue par SonarCloud.
/// </summary>
public sealed class ParcJsTests
{
    /// <summary>Node.js le plus ancien qui convient : modules détectés sans réglage, couverture limitée à un fichier.</summary>
    private static readonly Version MinimumNode = new(22, 12);

    /// <summary>Couverture du JavaScript (LCOV), relative à la racine du dépôt (sonar.javascript.lcov.reportPaths de ci.yml).</summary>
    internal const string LcovFile = "MonsutaPotaru.Tests/TestResults/parc-js.lcov";

    private const string Module = "MonsutaPotaru/wwwroot/js/parc-modele.js";
    private const string Tests = "MonsutaPotaru.Tests/js/*.test.mjs";

    [Fact]
    public void ParkModel_PassesItsNodeTests()
    {
        var root = Path.GetFullPath(Path.Combine(RealSavesTests.WebRoot(), "..", ".."));
        var version = Run(root, TimeSpan.FromSeconds(30), "--version");
        Assert.True(version.ExitCode == 0 && Version.TryParse(version.Output.Trim().TrimStart('v'), out var node) && node >= MinimumNode,
            $"Node.js {MinimumNode} ou plus récent est nécessaire pour les tests du parc (node --version : « {version.Output.Trim()} »).");

        Directory.CreateDirectory(Path.Combine(root, Path.GetDirectoryName(LcovFile)!));
        var run = Run(root, TimeSpan.FromMinutes(2),
            "--test", "--experimental-test-coverage", $"--test-coverage-include={Module}",
            "--test-reporter=spec", "--test-reporter-destination=stdout",
            "--test-reporter=lcov", $"--test-reporter-destination={LcovFile}",
            Tests);
        Assert.True(run.ExitCode == 0, $"Tests JavaScript du parc en échec (node --test {Tests}) :\n{run.Output}");
        Assert.Contains("parc-modele.js", File.ReadAllText(Path.Combine(root, LcovFile)));
    }

    /// <summary>Lance Node dans <paramref name="folder"/> ; rend son code de sortie et tout ce qu'il a écrit (sortie et erreurs).</summary>
    private static (int ExitCode, string Output) Run(string folder, TimeSpan timeout, params string[] arguments)
    {
        var start = new ProcessStartInfo("node")
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        start.Environment["NO_COLOR"] = "1"; // pas de codes de couleur dans le message d'un échec

        Process process;
        try
        {
            process = Process.Start(start)!;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return (-1, "Node.js introuvable (commande « node »)");
        }
        using (process)
        {
            var output = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (!process.WaitForExit(timeout))
            {
                process.Kill(entireProcessTree: true);
                return (-1, $"Node.js ne s'est pas arrêté en {timeout.TotalSeconds} s.\n{output}");
            }
            process.WaitForExit(); // fin de la lecture des sorties
            lock (output)
                return (process.ExitCode, output.ToString());
        }
    }
}
