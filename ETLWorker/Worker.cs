using ETLWorker.Services;

namespace ETLWorker;

public sealed class EtlWorker : BackgroundService
{
    private readonly IServiceProvider   _sp;
    private readonly ILogger<EtlWorker> _log;
    private readonly TimeSpan           _interval;

    public EtlWorker(IServiceProvider   sp,
                     ILogger<EtlWorker> log,
                     IConfiguration     config)
    {
        _sp       = sp;
        _log      = log;
        _interval = TimeSpan.FromMinutes(
            config.GetValue<int>("Worker:IntervalMinutes", 60));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("+------------------------------------------+");
        _log.LogInformation("¦   ETL Worker Service — .NET 8            ¦");
        _log.LogInformation("¦   Proyecto: analisisopiniones (MySQL)    ¦");
        _log.LogInformation("+------------------------------------------+");

        while (!stoppingToken.IsCancellationRequested)
        {
            _log.LogInformation("Iniciando ciclo ETL: {Time:yyyy-MM-dd HH:mm:ss}", DateTime.Now);
            try
            {
                // FASE 1 — Extraccion -> Staging
                var extraction = _sp.GetRequiredService<ExtractionService>();
                await extraction.RunAsync(stoppingToken);

                // FASE 2 — Carga de Dimensiones -> MySQL
                var loading = _sp.GetRequiredService<LoadingService>();
                await loading.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogCritical(ex, "Error fatal en el ciclo ETL.");
            }

            _log.LogInformation("Proxima ejecucion en {Min} minutos.", _interval.TotalMinutes);
            await Task.Delay(_interval, stoppingToken);
        }

        _log.LogInformation("ETL Worker detenido.");
    }
}
