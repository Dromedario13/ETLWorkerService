using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ETLWorker.Abstractions;
using ETLWorker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETLWorker.Extractors;

public sealed class CsvExtractorOptions
{
    public string ClientesPath    { get; set; } = "data/clients.csv";
    public string FuentesPath     { get; set; } = "data/fuente_datos.csv";
    public string ProductosPath   { get; set; } = "data/products.csv";
    public string ComentariosPath { get; set; } = "data/social_comments.csv";
    public string EncuestasPath   { get; set; } = "data/surveys_part1.csv";
    public string ReviewsPath     { get; set; } = "data/web_reviews.csv";
}

public sealed class CsvExtractor :
    IExtractor<Cliente>,
    IExtractor<Fuente>,
    IExtractor<Producto>,
    IExtractor<Encuesta>,
    IExtractor<ComentarioSocial>,
    IExtractor<ReviewWeb>
{
    private readonly CsvExtractorOptions _opts;
    private readonly ILogger<CsvExtractor> _log;

    string IExtractor<Cliente>.SourceName          => "CSV:clients";
    string IExtractor<Fuente>.SourceName           => "CSV:fuente_datos";
    string IExtractor<Producto>.SourceName         => "CSV:products";
    string IExtractor<Encuesta>.SourceName         => "CSV:surveys_part1";
    string IExtractor<ComentarioSocial>.SourceName => "CSV:social_comments";
    string IExtractor<ReviewWeb>.SourceName        => "CSV:web_reviews";

    public CsvExtractor(IOptions<CsvExtractorOptions> opts,
                        ILogger<CsvExtractor> log)
    {
        _opts = opts.Value;
        _log  = log;
    }

    Task<IReadOnlyList<Cliente>> IExtractor<Cliente>.ExtractAsync(CancellationToken ct) =>
        Task.Run(() => (IReadOnlyList<Cliente>)Leer<Cliente, MapCliente>(_opts.ClientesPath), ct);

    Task<IReadOnlyList<Fuente>> IExtractor<Fuente>.ExtractAsync(CancellationToken ct) =>
        Task.Run(() => (IReadOnlyList<Fuente>)Leer<Fuente, MapFuente>(_opts.FuentesPath), ct);

    Task<IReadOnlyList<Producto>> IExtractor<Producto>.ExtractAsync(CancellationToken ct) =>
        Task.Run(() => (IReadOnlyList<Producto>)Leer<Producto, MapProducto>(_opts.ProductosPath), ct);

    Task<IReadOnlyList<Encuesta>> IExtractor<Encuesta>.ExtractAsync(CancellationToken ct) =>
        Task.Run(() => (IReadOnlyList<Encuesta>)Leer<Encuesta, MapEncuesta>(_opts.EncuestasPath), ct);

    Task<IReadOnlyList<ComentarioSocial>> IExtractor<ComentarioSocial>.ExtractAsync(CancellationToken ct) =>
        Task.Run(() => (IReadOnlyList<ComentarioSocial>)Leer<ComentarioSocial, MapComentario>(_opts.ComentariosPath), ct);

    Task<IReadOnlyList<ReviewWeb>> IExtractor<ReviewWeb>.ExtractAsync(CancellationToken ct) =>
        Task.Run(() => (IReadOnlyList<ReviewWeb>)Leer<ReviewWeb, MapReviewWeb>(_opts.ReviewsPath), ct);

    private List<T> Leer<T, TMap>(string ruta) where TMap : ClassMap<T>
    {
        var sw = Stopwatch.StartNew();

        if (!File.Exists(ruta))
        {
            _log.LogWarning("[CsvExtractor] Archivo no encontrado: {Ruta}", ruta);
            return new List<T>();
        }

        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord   = true,
            MissingFieldFound = null,
            BadDataFound      = null,
            TrimOptions       = TrimOptions.Trim,
            Encoding          = System.Text.Encoding.UTF8
        };

        using var reader = new StreamReader(ruta, System.Text.Encoding.UTF8);
        using var csv    = new CsvReader(reader, cfg);
        csv.Context.RegisterClassMap<TMap>();
        var lista = csv.GetRecords<T>().ToList();

        sw.Stop();
        _log.LogInformation("[CsvExtractor] {Archivo,-30} -> {Count,5} filas  ({Ms} ms)",
            Path.GetFileName(ruta), lista.Count, sw.ElapsedMilliseconds);
        return lista;
    }

    private sealed class MapCliente : ClassMap<Cliente>
    {
        public MapCliente()
        {
            Map(m => m.IdCliente).Name("IdCliente");
            Map(m => m.Nombre).Name("Nombre");
            Map(m => m.Email).Name("Email").Optional();
        }
    }

    private sealed class MapFuente : ClassMap<Fuente>
    {
        public MapFuente()
        {
            Map(m => m.IdFuente).Name("IdFuente");
            Map(m => m.TipoFuente).Name("TipoFuente");
            Map(m => m.FechaCarga).Name("FechaCarga").Optional();
        }
    }

    private sealed class MapProducto : ClassMap<Producto>
    {
        public MapProducto()
        {
            Map(m => m.IdProducto).Name("IdProducto");
            Map(m => m.Nombre).Name("Nombre");
            Map(m => m.Categoria).Name("Categoria").Optional();
        }
    }

    private sealed class MapComentario : ClassMap<ComentarioSocial>
    {
        public MapComentario()
        {
            Map(m => m.IdComment).Name("IdComment");
            Map(m => m.IdCliente).Name("IdCliente").Optional();
            Map(m => m.IdProducto).Name("IdProducto").Optional();
            Map(m => m.Fuente).Name("Fuente").Optional();
            Map(m => m.Fecha).Name("Fecha").Optional();
            Map(m => m.Comentario).Name("Comentario").Optional();
        }
    }

    private sealed class MapReviewWeb : ClassMap<ReviewWeb>
    {
        public MapReviewWeb()
        {
            Map(m => m.IdReview).Name("IdReview");
            Map(m => m.IdCliente).Name("IdCliente").Optional();
            Map(m => m.IdProducto).Name("IdProducto").Optional();
            Map(m => m.Fecha).Name("Fecha").Optional();
            Map(m => m.Comentario).Name("Comentario").Optional();
            Map(m => m.Rating).Name("Rating").Optional();
        }
    }

    private sealed class MapEncuesta : ClassMap<Encuesta>
    {
        public MapEncuesta()
        {
            Map(m => m.IdOpinion).Name("IdOpinion");
            Map(m => m.IdCliente).Name("IdCliente");
            Map(m => m.IdProducto).Name("IdProducto");
            Map(m => m.Fecha).Name("Fecha").Optional();
            Map(m => m.Comentario).Name("Comentario").Optional();
            Map(m => m.Clasificacion).Name("Clasificacion").Optional();
            Map(m => m.PuntajeSatisfaccion).Name("PuntajeSatisfaccion").Optional();
            Map(m => m.FuenteEncuesta).Name("Fuente").Optional();
        }
    }
}
