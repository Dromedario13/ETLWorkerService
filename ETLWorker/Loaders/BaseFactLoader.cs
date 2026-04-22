using ETLWorker.Abstractions;
using ETLWorker.Models;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETLWorker.Loaders
{
    // -- Base para Fact Loaders (con truncado previo) ---------------------------

    public abstract class BaseFactLoader : BaseLoader
    {
        protected BaseFactLoader(DimensionLoaderOptions opts, ILogger log)
            : base(opts, log) { }

        /// <summary>
        /// Limpia (trunca) la tabla fact antes de cargarla.
        /// TRUNCATE TABLE es más rápido que DELETE y reinicia los auto-incrementos.
        /// </summary>
        protected async Task TruncarTablaAsync(MySqlConnection conn, string tabla,
                                                CancellationToken ct = default)
        {
            _log.LogInformation("[FactLoader] Truncando tabla: {Tabla} ...", tabla);

            // Deshabilitar FK checks para poder truncar tablas referenciadas
            using var cmdOff = new MySqlCommand("SET FOREIGN_KEY_CHECKS = 0;", conn);
            await cmdOff.ExecuteNonQueryAsync(ct);

            using var cmdTrunc = new MySqlCommand($"TRUNCATE TABLE `{tabla}`;", conn);
            await cmdTrunc.ExecuteNonQueryAsync(ct);

            using var cmdOn = new MySqlCommand("SET FOREIGN_KEY_CHECKS = 1;", conn);
            await cmdOn.ExecuteNonQueryAsync(ct);

            _log.LogInformation("[FactLoader] Tabla {Tabla} limpiada exitosamente.", tabla);
        }
    }

    // -- Fact: encuesta ---------------------------------------------------------

    /// <summary>
    /// Carga la fact table <c>encuesta</c>.
    /// Proceso:
    ///   1. Lee los datos del área de staging (encuestas.json).
    ///   2. Trunca la tabla encuesta (limpieza previa a la carga).
    ///   3. Inserta cada registro con los campos:
    ///      id_opinion, id_cliente, id_producto, fecha, comentario,
    ///      clasificacion, puntaje_satisfaccion, fuente.
    /// </summary>
    public sealed class FactEncuestaLoader : BaseFactLoader, IDimensionLoader
    {
        public string TableName => "fact_encuesta";

        public FactEncuestaLoader(IOptions<DimensionLoaderOptions> opts,
                                   ILogger<FactEncuestaLoader> log)
            : base(opts.Value, log) { }

        public async Task<int> LoadAsync(CancellationToken ct = default)
        {
            var lista = LeerStaging<Encuesta>("encuestas");
            if (lista.Count == 0)
            {
                _log.LogWarning("[FactLoader] encuesta: staging vacío, se omite la carga.");
                return 0;
            }

            using var conn = AbrirConexion();
            await conn.OpenAsync(ct);

            // -- Paso 1: Limpiar la tabla --
            await TruncarTablaAsync(conn, "encuesta", ct);

            // -- Paso 2: Insertar registros --
            int insertados = 0;
            const string sql = @"
            INSERT INTO encuesta
                (id_opinion, id_cliente, id_producto, fecha,
                 comentario, clasificacion, puntaje_satisfaccion, fuente)
            VALUES
                (@id, @cli, @prod, @fecha,
                 @com, @clas, @puntaje, @fuente);";

            foreach (var e in lista)
            {
                DateTime? fecha = null;
                if (DateTime.TryParse(e.Fecha, out var dt)) fecha = dt;

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", e.IdOpinion);
                cmd.Parameters.AddWithValue("@cli", e.IdCliente);
                cmd.Parameters.AddWithValue("@prod", e.IdProducto);
                cmd.Parameters.AddWithValue("@fecha", (object?)fecha ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@com", (object?)e.Comentario ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@clas", (object?)e.Clasificacion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@puntaje", (object?)e.PuntajeSatisfaccion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fuente", (object?)e.FuenteEncuesta ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
                insertados++;
            }

            _log.LogInformation("[FactLoader] encuesta          -> {N,5} registros cargados", insertados);
            return insertados;
        }
    }

    // -- Fact: comentarios_sociales ---------------------------------------------

    /// <summary>
    /// Carga la fact table <c>comentarios_sociales</c>.
    /// Proceso:
    ///   1. Lee los datos del área de staging (comentarios_sociales.json).
    ///   2. Trunca la tabla comentarios_sociales (limpieza previa a la carga).
    ///   3. Inserta cada registro con los campos:
    ///      id_comment, id_cliente, id_producto, fuente, fecha, comentario.
    ///   4. Los campos id_cliente e id_producto se convierten a INT cuando es posible;
    ///      de lo contrario se registran como NULL.
    /// </summary>
    public sealed class FactComentariosSocialesLoader : BaseFactLoader, IDimensionLoader
    {
        public string TableName => "fact_comentarios_sociales";

        public FactComentariosSocialesLoader(IOptions<DimensionLoaderOptions> opts,
                                              ILogger<FactComentariosSocialesLoader> log)
            : base(opts.Value, log) { }

        public async Task<int> LoadAsync(CancellationToken ct = default)
        {
            var lista = LeerStaging<ComentarioSocial>("comentarios_sociales");
            if (lista.Count == 0)
            {
                _log.LogWarning("[FactLoader] comentarios_sociales: staging vacío, se omite la carga.");
                return 0;
            }

            using var conn = AbrirConexion();
            await conn.OpenAsync(ct);

            // -- Paso 1: Limpiar la tabla --
            await TruncarTablaAsync(conn, "comentarios_sociales", ct);

            // -- Paso 2: Insertar registros --
            int insertados = 0;
            const string sql = @"
            INSERT INTO comentarios_sociales
                (id_comment, id_cliente, id_producto, fuente, fecha, comentario)
            VALUES
                (@id, @cli, @prod, @fuente, @fecha, @com);";

            foreach (var c in lista)
            {
                DateTime? fecha = null;
                if (DateTime.TryParse(c.Fecha, out var dt)) fecha = dt;

                int? idCli = int.TryParse(c.IdCliente, out var cli) ? cli : (int?)null;
                int? idProd = int.TryParse(c.IdProducto, out var prod) ? prod : (int?)null;

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", c.IdComment);
                cmd.Parameters.AddWithValue("@cli", (object?)idCli ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@prod", (object?)idProd ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fuente", (object?)c.Fuente ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fecha", (object?)fecha ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@com", (object?)c.Comentario ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
                insertados++;
            }

            _log.LogInformation("[FactLoader] comentarios_sociales -> {N,5} registros cargados", insertados);
            return insertados;
        }
    }

    // -- Fact: review_web -------------------------------------------------------

    /// <summary>
    /// Carga la fact table <c>review_web</c>.
    /// Proceso:
    ///   1. Lee los datos del área de staging (reviews_web.json).
    ///   2. Trunca la tabla review_web (limpieza previa a la carga).
    ///   3. Inserta cada registro con los campos:
    ///      id_review, id_cliente, id_producto, fecha, comentario, rating.
    ///   4. Los campos id_cliente e id_producto se convierten a INT cuando es posible.
    /// </summary>
    public sealed class FactReviewWebLoader : BaseFactLoader, IDimensionLoader
    {
        public string TableName => "fact_review_web";

        public FactReviewWebLoader(IOptions<DimensionLoaderOptions> opts,
                                    ILogger<FactReviewWebLoader> log)
            : base(opts.Value, log) { }

        public async Task<int> LoadAsync(CancellationToken ct = default)
        {
            var lista = LeerStaging<ReviewWeb>("reviews_web");
            if (lista.Count == 0)
            {
                _log.LogWarning("[FactLoader] review_web: staging vacío, se omite la carga.");
                return 0;
            }

            using var conn = AbrirConexion();
            await conn.OpenAsync(ct);

            // -- Paso 1: Limpiar la tabla --
            await TruncarTablaAsync(conn, "review_web", ct);

            // -- Paso 2: Insertar registros --
            int insertados = 0;
            const string sql = @"
            INSERT INTO review_web
                (id_review, id_cliente, id_producto, fecha, comentario, rating)
            VALUES
                (@id, @cli, @prod, @fecha, @com, @rating);";

            foreach (var r in lista)
            {
                DateTime? fecha = null;
                if (DateTime.TryParse(r.Fecha, out var dt)) fecha = dt;

                int? idCli = int.TryParse(r.IdCliente, out var cli) ? cli : (int?)null;
                int? idProd = int.TryParse(r.IdProducto, out var prod) ? prod : (int?)null;

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", r.IdReview);
                cmd.Parameters.AddWithValue("@cli", (object?)idCli ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@prod", (object?)idProd ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fecha", (object?)fecha ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@com", (object?)r.Comentario ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rating", (object?)r.Rating ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
                insertados++;
            }

            _log.LogInformation("[FactLoader] review_web        -> {N,5} registros cargados", insertados);
            return insertados;
        }

    }
}
