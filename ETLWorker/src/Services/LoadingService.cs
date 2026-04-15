using System.Diagnostics;
using ETLWorker.Abstractions;
using Microsoft.Extensions.Logging;

namespace ETLWorker.Services;

public sealed class LoadingService
{
    private readonly IEnumerable<IDimensionLoader> _loaders;
    private readonly ILogger<LoadingService>        _log;

    public LoadingService(IEnumerable<IDimensionLoader> loaders,
                          ILogger<LoadingService>        log)
    {
        _loaders = loaders;
        _log     = log;
    }

    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _log.LogInformation("");
        _log.LogInformation("*** FASE 2: CARGA DE DIMENSIONES ***");

        int total = 0;

        var orden = new[]
        {
            "clientes", "productos", "fuente",
            "encuesta", "comentarios_sociales", "review_web"
        };

        foreach (var tabla in orden)
        {
            var loader = _loaders.FirstOrDefault(l => l.TableName == tabla);
            if (loader is null)
            {
                _log.LogWarning("[LoadingService] Sin loader registrado para: {Tabla}", tabla);
                continue;
            }

            try
            {
                var n = await loader.LoadAsync(ct);
                total += n;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[LoadingService] Error cargando {Tabla}: {Msg}",
                    tabla, ex.Message);
            }
        }

        sw.Stop();
        _log.LogInformation("");
        _log.LogInformation("*** CARGA COMPLETADA en {S:F2} seg - {Total} registros ***",
            sw.Elapsed.TotalSeconds, total);

        return total;
    }
}
