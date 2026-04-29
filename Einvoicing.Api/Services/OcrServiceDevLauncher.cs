using System.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Einvoicing.Api.Services;

/// <summary>
/// Dev helper: démarre automatiquement le microservice OCR local si besoin.
/// En production, l'OCR doit être orchestré séparément (Docker/service).
/// </summary>
public static class OcrServiceDevLauncher
{
    public static async Task EnsureRunningAsync(
        IConfiguration config,
        IHostEnvironment env,
        ILogger logger,
        CancellationToken appStopping)
    {
        if (!env.IsDevelopment())
            return;

        var autoStart = config.GetValue("OcrService:AutoStart", true);
        if (!autoStart)
            return;

        var baseUrl = config["OcrService:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            return;

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            logger.LogWarning("OcrService:BaseUrl invalide: {BaseUrl}", baseUrl);
            return;
        }

        // On n'auto-start que pour un endpoint local.
        if (!(uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host == "127.0.0.1"))
            return;

        var healthUrl = $"{baseUrl.TrimEnd('/')}/health";
        if (await IsHealthyAsync(healthUrl))
            return;

        var workDir = ResolveWorkDir(config, env.ContentRootPath);
        if (string.IsNullOrWhiteSpace(workDir) || !Directory.Exists(workDir))
        {
            logger.LogWarning("OCR workdir introuvable: {WorkDir}", workDir);
            return;
        }

        var pythonExe = ResolvePythonExe(config, workDir);
        if (string.IsNullOrWhiteSpace(pythonExe) || !File.Exists(pythonExe))
        {
            logger.LogWarning("Python introuvable pour OCR: {PythonExe}", pythonExe);
            return;
        }

        var host = uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ? "127.0.0.1" : uri.Host;
        var port = uri.Port;
        var args = $"-m uvicorn main:app --host {host} --port {port}";

        try
        {
            logger.LogInformation("Démarrage OCR service: {PythonExe} {Args}", pythonExe, args);

            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = args,
                WorkingDirectory = workDir,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.Environment["PYTHONUNBUFFERED"] = "1";
            psi.Environment["PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK"] = "True";

            var proc = Process.Start(psi);
            if (proc is null)
            {
                logger.LogWarning("Impossible de démarrer le process OCR.");
                return;
            }

            appStopping.Register(() =>
            {
                try
                {
                    if (!proc.HasExited)
                        proc.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignore
                }
            });

            // On attend brièvement le /health (le 1er démarrage peut être un peu long).
            for (var i = 0; i < 20; i++)
            {
                if (await IsHealthyAsync(healthUrl))
                {
                    logger.LogInformation("OCR service prêt: {HealthUrl}", healthUrl);
                    return;
                }

                await Task.Delay(500);
            }

            logger.LogInformation("OCR service démarré, /health pas encore prêt (patientez quelques secondes): {HealthUrl}", healthUrl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Échec démarrage OCR service");
        }
    }

    private static async Task<bool> IsHealthyAsync(string healthUrl)
    {
        try
        {
            using var http = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(900)
            };

            using var resp = await http.GetAsync(healthUrl);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string? ResolveWorkDir(IConfiguration config, string contentRoot)
    {
        var cfg = config["OcrService:WorkDir"];
        if (!string.IsNullOrWhiteSpace(cfg))
        {
            return Path.GetFullPath(Path.Combine(contentRoot, cfg));
        }

        // Remonte l'arborescence pour trouver un dossier "ocr_service".
        var dir = new DirectoryInfo(contentRoot);
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir.FullName, "ocr_service");
            if (Directory.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        // Fallback classique.
        return Path.GetFullPath(Path.Combine(contentRoot, "..", "ocr_service"));
    }

    private static string? ResolvePythonExe(IConfiguration config, string workDir)
    {
        var cfg = config["OcrService:PythonExe"];
        if (!string.IsNullOrWhiteSpace(cfg))
            return cfg;

        if (OperatingSystem.IsWindows())
            return Path.Combine(workDir, ".venv", "Scripts", "python.exe");

        return Path.Combine(workDir, ".venv", "bin", "python");
    }
}