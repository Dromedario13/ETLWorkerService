using System.Text.Json;
using ETLWorker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETLWorker.Staging;

// ── Opciones ──────────────────────────────────────────────────────────────

public sealed class StagingOptions
{
    public string Directory { get; set; } = "staging";
}

// ── StagingService ────────────────────────────────────────────────────────

/// <summary>
/// Persiste los datos extraídos en archivos JSON temporales (área staging).
/// Desacopla la fase de Extracción de la fase de Transformación/Carga,
/// permitiendo reintentar una fase sin repetir la anterior.
/// </summary>
public sealed class StagingService
{
    private readonly string               _dir;
    private readonly ILogger<StagingService> _log;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { WriteIndented = true };

    public StagingService(IOptions<StagingOptions> opts,
                          ILogger<StagingService>  log)
    {
        _dir = opts.Value.Directory;
        _log = log;
        Directory.CreateDirectory(_dir);
    }

    public async Task GuardarAsync<T>(string nombre, IEnumerable<T> datos,
                                       CancellationToken ct = default)
    {
        var lista    = datos.ToList();
        var filePath = Path.Combine(_dir, $"{nombre}.json");

        await using var fs = File.Create(filePath);
        await JsonSerializer.SerializeAsync(fs, lista, JsonOpts, ct);

        _log.LogInformation("[Staging] {Nombre,-30} → {Path}  ({Count} registros)",
            nombre, filePath, lista.Count);
    }

    public async Task GuardarTodoAsync(ExtractionResult result, CancellationToken ct = default)
    {
        await GuardarAsync("clientes",           result.Clientes,    ct);
        await GuardarAsync("fuentes",            result.Fuentes,     ct);
        await GuardarAsync("productos",          result.Productos,   ct);
        await GuardarAsync("encuestas",          result.Encuestas,   ct);
        await GuardarAsync("comentarios_sociales", result.Comentarios, ct);
        await GuardarAsync("reviews_web",        result.Reviews,     ct);
    }
}
