using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Paperless.Modules.Setup;

/// <summary>
/// Camada de baixo nível para interagir com o Ollama fora do fluxo normal de chat:
/// detecção de instalação, start do servidor, inventário/pull de modelos.
/// </summary>
public sealed class OllamaBootstrap
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public OllamaBootstrap(HttpClient http, string baseUrl)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    /// <summary>
    /// True se o binário `ollama` existe no PATH do sistema.
    /// </summary>
    public async Task<bool> IsBinaryInstalledAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ollama",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch
        {
            // Binário não encontrado, permissão negada, etc. Tratamos como "não instalado".
            return false;
        }
    }

    /// <summary>
    /// True se o servidor HTTP do Ollama está respondendo na BaseUrl configurada.
    /// Tem timeout curto (3s) para não travar o startup se a porta estiver morta.
    /// </summary>
    public async Task<bool> IsServerRunningAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            using var resp = await _http.GetAsync($"{_baseUrl}/api/tags", cts.Token);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Lista os modelos atualmente baixados no Ollama (nomes completos, ex: "llama3.2:3b").
    /// </summary>
    public async Task<IReadOnlyList<string>> ListInstalledModelsAsync(CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync($"{_baseUrl}/api/tags", ct);
        if (!resp.IsSuccessStatusCode)
            return Array.Empty<string>();

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("models", out var models))
            return Array.Empty<string>();

        var names = new List<string>();
        foreach (var m in models.EnumerateArray())
            if (m.TryGetProperty("name", out var n))
                names.Add(n.GetString() ?? "");

        return names;
    }

    /// <summary>
    /// Verifica se um modelo está instalado. Aceita com ou sem tag
    /// ("llama3.2" casa com "llama3.2:3b"; "nomic-embed-text" casa com "nomic-embed-text:latest").
    /// </summary>
    public async Task<bool> IsModelInstalledAsync(string model, CancellationToken ct = default)
    {
        var installed = await ListInstalledModelsAsync(ct);
        return installed.Any(m =>
            m.Equals(model, StringComparison.OrdinalIgnoreCase) ||
            m.StartsWith(model + ":", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Faz `ollama pull &lt;model&gt;` via API, reportando progresso.
    /// </summary>
    public async Task<bool> PullModelAsync(
        string model,
        IProgress<PullProgress>? progress = null,
        CancellationToken ct = default)
    {
        var payload = JsonContent.Create(new { name = model, stream = true });
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/pull")
        {
            Content = payload,
        };

        using var resp = await _http.SendAsync(
            req, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!resp.IsSuccessStatusCode)
            return false;

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var status = root.TryGetProperty("status", out var s)
                    ? s.GetString() ?? ""
                    : "";
                long completed = root.TryGetProperty("completed", out var c)
                    ? c.GetInt64() : 0;
                long total = root.TryGetProperty("total", out var t)
                    ? t.GetInt64() : 0;

                progress?.Report(new PullProgress(status, completed, total));

                if (status.Equals("success", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (root.TryGetProperty("error", out var err))
                {
                    progress?.Report(new PullProgress(
                        $"erro: {err.GetString()}", completed, total));
                    return false;
                }
            }
            catch (JsonException)
            { /* Ignore */ }
        }

        return true;
    }

    // ─────────────────────────── Instalação ───────────────────────────

    /// <summary>
    /// Instala o Ollama usando o instalador oficial apropriado para o SO atual.
    /// Linux/macOS: curl ... | sh (pode pedir senha sudo).
    /// Windows: baixa OllamaSetup.exe e executa.
    /// </summary>
    public async Task<bool> InstallAsync(CancellationToken ct = default)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await InstallOnUnixAsync(ct);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await InstallOnWindowsAsync(ct);
        }

        return false;
    }

    private static async Task<bool> InstallOnUnixAsync(CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = "-c \"curl -fsSL https://ollama.com/install.sh | sh\"",
            UseShellExecute = false,
        };

        using var proc = Process.Start(psi);
        if (proc is null) return false;

        await proc.WaitForExitAsync(ct);
        return proc.ExitCode == 0;
    }

    private async Task<bool> InstallOnWindowsAsync(CancellationToken ct)
    {
        var installerPath = Path.Combine(Path.GetTempPath(), "OllamaSetup.exe");

        // Download
        using (var resp = await _http.GetAsync(
            "https://ollama.com/download/OllamaSetup.exe",
            HttpCompletionOption.ResponseHeadersRead, ct))
        {
            if (!resp.IsSuccessStatusCode) return false;
            await using var fs = System.IO.File.Create(installerPath);
            await resp.Content.CopyToAsync(fs, ct);
        }

        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true,
            Verb = "runas",
        };

        using var proc = Process.Start(psi);
        if (proc is null) return false;

        await proc.WaitForExitAsync(ct);
        return proc.ExitCode == 0;
    }
}

public readonly record struct PullProgress(string Status, long Completed, long Total);