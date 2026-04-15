using System.Diagnostics;
using System.Net.Http.Json;
using ETLWorker.Abstractions;
using ETLWorker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETLWorker.Extractors;

// ── Opciones ──────────────────────────────────────────────────────────────

public sealed class ApiExtractorOptions
{
    public string BaseUrl  { get; set; } = "https://api.ejemplo.com";
    public string Endpoint { get; set; } = "/v1/social-comments";
    /// <summary>Se inyecta desde User Secrets o variable de entorno. Nunca hardcodeada.</summary>
    public string ApiKey   { get; set; } = "";
    public int    PageSize { get; set; } = 100;
}

// ── DTO de respuesta ──────────────────────────────────────────────────────

internal sealed record ApiComentarioDto(
    string  id_comment,
    string? id_cliente,
    string? id_producto,
    string? fuente,
    string? fecha,
    string? comentario);

// ── ApiExtractor ──────────────────────────────────────────────────────────

/// <summary>
/// Consume la API REST de comentarios sociales mediante IHttpClientFactory.
/// Implementa paginación automática y fallo suave ante errores HTTP.
/// La ApiKey se envía como header — nunca en la URL.
/// </summary>
public sealed class ApiExtractor : IExtractor<ComentarioSocial>
{
    public string SourceName => "API:social-comments";

    private readonly System.Net.Http.IHttpClientFactory _httpFactory;
    private readonly ApiExtractorOptions                _opts;
    private readonly ILogger<ApiExtractor>              _log;

    public ApiExtractor(System.Net.Http.IHttpClientFactory httpFactory,
                        IOptions<ApiExtractorOptions>      opts,
                        ILogger<ApiExtractor>              log)
    {
        _httpFactory = httpFactory;
        _opts        = opts.Value;
        _log         = log;
    }

    public async Task<IReadOnlyList<ComentarioSocial>> ExtractAsync(CancellationToken ct = default)
    {
        // Si la URL es un placeholder, no intentar la llamada
        if (_opts.BaseUrl.Contains("ejemplo.com", StringComparison.OrdinalIgnoreCase))
        {
            _log.LogWarning("[ApiExtractor] BaseUrl es placeholder — skipping.");
            return Array.Empty<ComentarioSocial>();
        }

        var sw     = Stopwatch.StartNew();
        var client = _httpFactory.CreateClient("ApiExtractor");

        // Seguridad: API Key como header, nunca en la URL
        if (!string.IsNullOrWhiteSpace(_opts.ApiKey))
            client.DefaultRequestHeaders.Add("X-Api-Key", _opts.ApiKey);

        _log.LogInformation("[ApiExtractor] Iniciando extracción paginada. PageSize={PS}",
            _opts.PageSize);

        var todos  = new List<ComentarioSocial>();
        int pagina = 1;

        while (true)
        {
            var url = $"{_opts.BaseUrl}{_opts.Endpoint}?page={pagina}&size={_opts.PageSize}";

            List<ApiComentarioDto>? dtos;
            try
            {
                dtos = await client.GetFromJsonAsync<List<ApiComentarioDto>>(url, ct);
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                // Fallo suave: loguear y detener paginación
                _log.LogWarning("[ApiExtractor] Error HTTP página {P}: {Msg}", pagina, ex.Message);
                break;
            }

            if (dtos is null || dtos.Count == 0) break;

            todos.AddRange(dtos.Select(d => new ComentarioSocial
            {
                IdComment  = d.id_comment,
                IdCliente  = d.id_cliente,
                IdProducto = d.id_producto,
                Fuente     = d.fuente,
                Fecha      = d.fecha,
                Comentario = d.comentario
            }));

            if (dtos.Count < _opts.PageSize) break; // última página
            pagina++;
        }

        sw.Stop();
        _log.LogInformation("[ApiExtractor] {Count,5} registros extraídos en {Ms} ms",
            todos.Count, sw.ElapsedMilliseconds);
        return todos;
    }
}
