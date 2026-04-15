using System.Diagnostics;
using ETLWorker.Abstractions;
using ETLWorker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;

namespace ETLWorker.Extractors;

// ── Opciones ──────────────────────────────────────────────────────────────

public sealed class DatabaseExtractorOptions
{
    public string Query { get; set; } =
        "SELECT id_review, id_cliente, id_producto, fecha, comentario, rating FROM review_web";
}

// ── DatabaseExtractor ─────────────────────────────────────────────────────

/// <summary>
/// Extrae ReviewWeb desde la base de datos MySQL usando ADO.NET.
/// La ConnectionString se lee desde IConfiguration (appsettings / User Secrets)
/// para no exponer credenciales en el código fuente.
/// </summary>
public sealed class DatabaseExtractor : IExtractor<ReviewWeb>
{
    public string SourceName => "Database:review_web";

    private readonly string                      _connStr;
    private readonly DatabaseExtractorOptions    _opts;
    private readonly ILogger<DatabaseExtractor>  _log;

    public DatabaseExtractor(IConfiguration                    config,
                              IOptions<DatabaseExtractorOptions> opts,
                              ILogger<DatabaseExtractor>         log)
    {
        _connStr = config.GetConnectionString("MySQL") ?? "";
        _opts    = opts.Value;
        _log     = log;
    }

    public async Task<IReadOnlyList<ReviewWeb>> ExtractAsync(CancellationToken ct = default)
    {
        // Seguridad: si no hay cadena de conexión configurada, salir limpiamente
        if (string.IsNullOrWhiteSpace(_connStr) ||
            _connStr.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
        {
            _log.LogWarning("[DatabaseExtractor] ConnectionString no configurada — skipping.");
            return Array.Empty<ReviewWeb>();
        }

        var sw    = Stopwatch.StartNew();
        var lista = new List<ReviewWeb>();

        _log.LogInformation("[DatabaseExtractor] Conectando a MySQL...");

        using var conn = new MySqlConnection(_connStr);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = _opts.Query;

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            lista.Add(new ReviewWeb
            {
                IdReview   = reader.GetString(0),
                IdCliente  = reader.IsDBNull(1) ? null : reader.GetInt32(1).ToString(),
                IdProducto = reader.IsDBNull(2) ? null : reader.GetInt32(2).ToString(),
                Fecha      = reader.IsDBNull(3) ? null : reader.GetDateTime(3).ToString("yyyy-MM-dd"),
                Comentario = reader.IsDBNull(4) ? null : reader.GetString(4),
                Rating     = reader.IsDBNull(5) ? null : reader.GetDouble(5)
            });
        }

        sw.Stop();
        _log.LogInformation("[DatabaseExtractor] {Count,5} filas extraídas en {Ms} ms",
            lista.Count, sw.ElapsedMilliseconds);
        return lista;
    }
}
