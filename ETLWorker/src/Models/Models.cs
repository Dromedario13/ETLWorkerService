namespace ETLWorker.Models;

// ── Modelos de dominio ────────────────────────────────────────────────────

public class Cliente
{
    public int     IdCliente { get; set; }
    public string  Nombre    { get; set; } = "";
    public string? Email     { get; set; }
}

public class Fuente
{
    public string  IdFuente   { get; set; } = "";
    public string  TipoFuente { get; set; } = "";
    public string? FechaCarga { get; set; }
}

public class Producto
{
    public int     IdProducto { get; set; }
    public string  Nombre     { get; set; } = "";
    public string? Categoria  { get; set; }
}

public class Encuesta
{
    public int     IdOpinion           { get; set; }
    public int     IdCliente           { get; set; }
    public int     IdProducto          { get; set; }
    public string? Fecha               { get; set; }
    public string? Comentario          { get; set; }
    public string? Clasificacion       { get; set; }
    public double? PuntajeSatisfaccion { get; set; }
    public string? FuenteEncuesta      { get; set; }
}

public class ComentarioSocial
{
    public string  IdComment  { get; set; } = "";
    public string? IdCliente  { get; set; }
    public string? IdProducto { get; set; }
    public string? Fuente     { get; set; }
    public string? Fecha      { get; set; }
    public string? Comentario { get; set; }
}

public class ReviewWeb
{
    public string  IdReview   { get; set; } = "";
    public string? IdCliente  { get; set; }
    public string? IdProducto { get; set; }
    public string? Fecha      { get; set; }
    public string? Comentario { get; set; }
    public double? Rating     { get; set; }
}

// ── Resultado unificado de extracción ─────────────────────────────────────

public class ExtractionResult
{
    public List<Cliente>        Clientes    { get; set; } = new();
    public List<Fuente>         Fuentes     { get; set; } = new();
    public List<Producto>       Productos   { get; set; } = new();
    public List<Encuesta>       Encuestas   { get; set; } = new();
    public List<ComentarioSocial> Comentarios { get; set; } = new();
    public List<ReviewWeb>      Reviews     { get; set; } = new();
}
