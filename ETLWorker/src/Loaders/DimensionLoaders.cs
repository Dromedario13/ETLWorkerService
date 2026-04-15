using System.Text.Json;
using ETLWorker.Abstractions;
using ETLWorker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;

namespace ETLWorker.Loaders;

public sealed class DimensionLoaderOptions
{
    public string ConnectionString { get; set; } = "";
    public string StagingDirectory { get; set; } = "staging";
}

public abstract class BaseLoader
{
    protected readonly string _connStr;
    protected readonly string _stagingDir;
    protected readonly ILogger _log;

    private static readonly JsonSerializerOptions _jsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    protected BaseLoader(DimensionLoaderOptions opts, ILogger log)
    {
        _connStr    = opts.ConnectionString;
        _stagingDir = opts.StagingDirectory;
        _log        = log;
    }

    protected List<T> LeerStaging<T>(string nombre)
    {
        var path = Path.Combine(_stagingDir, $"{nombre}.json");
        if (!File.Exists(path))
        {
            _log.LogWarning("[Loader] Archivo staging no encontrado: {Path}", path);
            return new();
        }
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<T>>(json, _jsonOpts) ?? new();
    }

    protected MySqlConnection AbrirConexion() => new(_connStr);
}

public sealed class ClientesLoader : BaseLoader, IDimensionLoader
{
    public string TableName => "clientes";

    public ClientesLoader(IOptions<DimensionLoaderOptions> opts, ILogger<ClientesLoader> log)
        : base(opts.Value, log) { }

    public async Task<int> LoadAsync(CancellationToken ct = default)
    {
        var lista = LeerStaging<Cliente>("clientes");
        if (lista.Count == 0) return 0;

        int insertados = 0;
        using var conn = AbrirConexion();
        await conn.OpenAsync(ct);

        foreach (var c in lista)
        {
            var sql = @"INSERT INTO clientes (id_cliente, nombre, email)
                        VALUES (@id, @nombre, @email)
                        ON DUPLICATE KEY UPDATE nombre = @nombre, email = @email;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id",     c.IdCliente);
            cmd.Parameters.AddWithValue("@nombre", c.Nombre);
            cmd.Parameters.AddWithValue("@email",  (object?)c.Email ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            insertados++;
        }

        _log.LogInformation("[Loader] clientes          -> {N,5} registros cargados", insertados);
        return insertados;
    }
}

public sealed class ProductosLoader : BaseLoader, IDimensionLoader
{
    public string TableName => "productos";

    public ProductosLoader(IOptions<DimensionLoaderOptions> opts, ILogger<ProductosLoader> log)
        : base(opts.Value, log) { }

    public async Task<int> LoadAsync(CancellationToken ct = default)
    {
        var lista = LeerStaging<Producto>("productos");
        if (lista.Count == 0) return 0;

        int insertados = 0;
        using var conn = AbrirConexion();
        await conn.OpenAsync(ct);

        foreach (var p in lista)
        {
            var sql = @"INSERT INTO productos (id_producto, nombre, categoria)
                        VALUES (@id, @nombre, @cat)
                        ON DUPLICATE KEY UPDATE nombre = @nombre, categoria = @cat;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id",     p.IdProducto);
            cmd.Parameters.AddWithValue("@nombre", p.Nombre);
            cmd.Parameters.AddWithValue("@cat",    (object?)p.Categoria ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            insertados++;
        }

        _log.LogInformation("[Loader] productos         -> {N,5} registros cargados", insertados);
        return insertados;
    }
}

public sealed class FuenteLoader : BaseLoader, IDimensionLoader
{
    public string TableName => "fuente";

    public FuenteLoader(IOptions<DimensionLoaderOptions> opts, ILogger<FuenteLoader> log)
        : base(opts.Value, log) { }

    public async Task<int> LoadAsync(CancellationToken ct = default)
    {
        var lista = LeerStaging<Fuente>("fuentes");
        if (lista.Count == 0) return 0;

        int insertados = 0;
        using var conn = AbrirConexion();
        await conn.OpenAsync(ct);

        foreach (var f in lista)
        {
            DateTime? fecha = null;
            if (DateTime.TryParse(f.FechaCarga, out var dt)) fecha = dt;

            var sql = @"INSERT INTO fuente (id_fuente, tipo_fuente, fecha_carga)
                        VALUES (@id, @tipo, @fecha)
                        ON DUPLICATE KEY UPDATE tipo_fuente = @tipo, fecha_carga = @fecha;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id",    f.IdFuente);
            cmd.Parameters.AddWithValue("@tipo",  f.TipoFuente);
            cmd.Parameters.AddWithValue("@fecha", (object?)fecha ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            insertados++;
        }

        _log.LogInformation("[Loader] fuente            -> {N,5} registros cargados", insertados);
        return insertados;
    }
}

public sealed class EncuestaLoader : BaseLoader, IDimensionLoader
{
    public string TableName => "encuesta";

    public EncuestaLoader(IOptions<DimensionLoaderOptions> opts, ILogger<EncuestaLoader> log)
        : base(opts.Value, log) { }

    public async Task<int> LoadAsync(CancellationToken ct = default)
    {
        var lista = LeerStaging<Encuesta>("encuestas");
        if (lista.Count == 0) return 0;

        int insertados = 0;
        using var conn = AbrirConexion();
        await conn.OpenAsync(ct);

        foreach (var e in lista)
        {
            DateTime? fecha = null;
            if (DateTime.TryParse(e.Fecha, out var dt)) fecha = dt;

            var sql = @"INSERT INTO encuesta
                            (id_opinion, id_cliente, id_producto, fecha,
                             comentario, clasificacion, puntaje_satisfaccion, fuente)
                        VALUES
                            (@id, @cli, @prod, @fecha,
                             @com, @clas, @puntaje, @fuente)
                        ON DUPLICATE KEY UPDATE
                            fecha                = @fecha,
                            comentario           = @com,
                            clasificacion        = @clas,
                            puntaje_satisfaccion = @puntaje,
                            fuente               = @fuente;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id",      e.IdOpinion);
            cmd.Parameters.AddWithValue("@cli",     e.IdCliente);
            cmd.Parameters.AddWithValue("@prod",    e.IdProducto);
            cmd.Parameters.AddWithValue("@fecha",   (object?)fecha ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@com",     (object?)e.Comentario ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@clas",    (object?)e.Clasificacion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@puntaje", (object?)e.PuntajeSatisfaccion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fuente",  (object?)e.FuenteEncuesta ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            insertados++;
        }

        _log.LogInformation("[Loader] encuesta          -> {N,5} registros cargados", insertados);
        return insertados;
    }
}

public sealed class ComentariosSocialesLoader : BaseLoader, IDimensionLoader
{
    public string TableName => "comentarios_sociales";

    public ComentariosSocialesLoader(IOptions<DimensionLoaderOptions> opts,
                                     ILogger<ComentariosSocialesLoader> log)
        : base(opts.Value, log) { }

    public async Task<int> LoadAsync(CancellationToken ct = default)
    {
        var lista = LeerStaging<ComentarioSocial>("comentarios_sociales");
        if (lista.Count == 0) return 0;

        int insertados = 0;
        using var conn = AbrirConexion();
        await conn.OpenAsync(ct);

        foreach (var c in lista)
        {
            DateTime? fecha = null;
            if (DateTime.TryParse(c.Fecha, out var dt)) fecha = dt;

            int? idCli  = int.TryParse(c.IdCliente,  out var cli)  ? cli  : null;
            int? idProd = int.TryParse(c.IdProducto, out var prod) ? prod : null;

            var sql = @"INSERT INTO comentarios_sociales
                            (id_comment, id_cliente, id_producto, fuente, fecha, comentario)
                        VALUES
                            (@id, @cli, @prod, @fuente, @fecha, @com)
                        ON DUPLICATE KEY UPDATE
                            id_cliente  = @cli,
                            id_producto = @prod,
                            fuente      = @fuente,
                            fecha       = @fecha,
                            comentario  = @com;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id",     c.IdComment);
            cmd.Parameters.AddWithValue("@cli",    (object?)idCli  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@prod",   (object?)idProd ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fuente", (object?)c.Fuente ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fecha",  (object?)fecha ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@com",    (object?)c.Comentario ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            insertados++;
        }

        _log.LogInformation("[Loader] comentarios_sociales -> {N,5} registros cargados", insertados);
        return insertados;
    }
}

public sealed class ReviewWebLoader : BaseLoader, IDimensionLoader
{
    public string TableName => "review_web";

    public ReviewWebLoader(IOptions<DimensionLoaderOptions> opts, ILogger<ReviewWebLoader> log)
        : base(opts.Value, log) { }

    public async Task<int> LoadAsync(CancellationToken ct = default)
    {
        var lista = LeerStaging<ReviewWeb>("reviews_web");
        if (lista.Count == 0) return 0;

        int insertados = 0;
        using var conn = AbrirConexion();
        await conn.OpenAsync(ct);

        foreach (var r in lista)
        {
            DateTime? fecha = null;
            if (DateTime.TryParse(r.Fecha, out var dt)) fecha = dt;

            int? idCli  = int.TryParse(r.IdCliente,  out var cli)  ? cli  : null;
            int? idProd = int.TryParse(r.IdProducto, out var prod) ? prod : null;

            var sql = @"INSERT INTO review_web
                            (id_review, id_cliente, id_producto, fecha, comentario, rating)
                        VALUES
                            (@id, @cli, @prod, @fecha, @com, @rating)
                        ON DUPLICATE KEY UPDATE
                            id_cliente  = @cli,
                            id_producto = @prod,
                            fecha       = @fecha,
                            comentario  = @com,
                            rating      = @rating;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id",     r.IdReview);
            cmd.Parameters.AddWithValue("@cli",    (object?)idCli  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@prod",   (object?)idProd ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fecha",  (object?)fecha ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@com",    (object?)r.Comentario ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@rating", (object?)r.Rating ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            insertados++;
        }

        _log.LogInformation("[Loader] review_web        -> {N,5} registros cargados", insertados);
        return insertados;
    }
}
