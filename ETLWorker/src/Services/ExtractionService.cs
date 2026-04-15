using System.Diagnostics;
using ETLWorker.Abstractions;
using ETLWorker.Models;
using ETLWorker.Staging;
using Microsoft.Extensions.Logging;

namespace ETLWorker.Services;

/// <summary>
/// Orquesta la fase de Extracción del pipeline ETL.
/// Ejecuta CsvExtractor, DatabaseExtractor y ApiExtractor en paralelo
/// usando Task.WhenAll para maximizar rendimiento en operaciones I/O.
/// </summary>
public sealed class ExtractionService
{
    // ── Extractores inyectados por DI ─────────────────────────────────────
    private readonly IExtractor<Cliente>         _clienteExtractor;
    private readonly IExtractor<Fuente>          _fuenteExtractor;
    private readonly IExtractor<Producto>        _productoExtractor;
    private readonly IExtractor<Encuesta>        _encuestaExtractor;
    private readonly IExtractor<ComentarioSocial> _comentarioExtractor;
    private readonly IExtractor<ReviewWeb>       _reviewExtractor;
    private readonly IExtractor<ComentarioSocial> _apiExtractor;

    private readonly StagingService              _staging;
    private readonly ILogger<ExtractionService>  _log;

    public ExtractionService(
        IExtractor<Cliente>          clienteExtractor,
        IExtractor<Fuente>           fuenteExtractor,
        IExtractor<Producto>         productoExtractor,
        IExtractor<Encuesta>         encuestaExtractor,
        IExtractor<ComentarioSocial> comentarioExtractor,
        IExtractor<ReviewWeb>        reviewExtractor,
        StagingService               staging,
        ILogger<ExtractionService>   log)
    {
        _clienteExtractor    = clienteExtractor;
        _fuenteExtractor     = fuenteExtractor;
        _productoExtractor   = productoExtractor;
        _encuestaExtractor   = encuestaExtractor;
        _comentarioExtractor = comentarioExtractor;
        _reviewExtractor     = reviewExtractor;
        _apiExtractor        = comentarioExtractor; // mismo tipo, fuente diferente
        _staging             = staging;
        _log                 = log;
    }

    public async Task<ExtractionResult> RunAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _log.LogInformation("");
        _log.LogInformation("*** FASE 1: EXTRACCION ***");

        // ── Catálogos CSV en paralelo ─────────────────────────────────────
        _log.LogInformation("[ExtractionService] Extrayendo catálogos (paralelo)...");
        var tClientes  = SafeExtract(_clienteExtractor,  ct);
        var tFuentes   = SafeExtract(_fuenteExtractor,   ct);
        var tProductos = SafeExtract(_productoExtractor, ct);
        await Task.WhenAll(tClientes, tFuentes, tProductos);

        // ── Datos de opinión en paralelo ──────────────────────────────────
        _log.LogInformation("[ExtractionService] Extrayendo opiniones (paralelo)...");
        var tEncuestas   = SafeExtract(_encuestaExtractor,   ct);
        var tComentarios = SafeExtract(_comentarioExtractor, ct);
        var tReviews     = SafeExtract(_reviewExtractor,     ct);
        await Task.WhenAll(tEncuestas, tComentarios, tReviews);

        var result = new ExtractionResult
        {
            Clientes    = tClientes.Result.ToList(),
            Fuentes     = tFuentes.Result.ToList(),
            Productos   = tProductos.Result.ToList(),
            Encuestas   = tEncuestas.Result.ToList(),
            Comentarios = tComentarios.Result.ToList(),
            Reviews     = tReviews.Result.ToList()
        };

        // ── Guardar en staging ────────────────────────────────────────────
        _log.LogInformation("[ExtractionService] Guardando en área staging...");
        await _staging.GuardarTodoAsync(result, ct);

        sw.Stop();
        _log.LogInformation("");
        _log.LogInformation("*** EXTRACCIÓN COMPLETADA en {S:F2} seg ***", sw.Elapsed.TotalSeconds);
        _log.LogInformation("  clientes             : {N,5}", result.Clientes.Count);
        _log.LogInformation("  fuentes              : {N,5}", result.Fuentes.Count);
        _log.LogInformation("  productos            : {N,5}", result.Productos.Count);
        _log.LogInformation("  encuestas            : {N,5}", result.Encuestas.Count);
        _log.LogInformation("  comentarios_sociales : {N,5}", result.Comentarios.Count);
        _log.LogInformation("  reviews_web          : {N,5}", result.Reviews.Count);

        return result;
    }

    // ── Fallo suave: si un extractor falla, los demás continúan ──────────
    private async Task<IReadOnlyList<T>> SafeExtract<T>(
        IExtractor<T> extractor, CancellationToken ct)
    {
        try
        {
            return await extractor.ExtractAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[ExtractionService] Error en {Source}: {Msg}",
                extractor.SourceName, ex.Message);
            return Array.Empty<T>();
        }
    }
}
