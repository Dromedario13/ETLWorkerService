namespace ETLWorker.Abstractions;

/// <summary>
/// Contrato genérico para todos los extractores del pipeline ETL.
/// Principio: Open/Closed — se pueden agregar nuevas fuentes sin modificar
/// el orquestador existente.
/// </summary>
/// <typeparam name="T">Tipo de modelo que retorna este extractor.</typeparam>
public interface IExtractor<T>
{
    /// <summary>Nombre descriptivo de la fuente (para logs y métricas).</summary>
    string SourceName { get; }

    /// <summary>
    /// Extrae registros de la fuente de datos de forma asíncrona.
    /// Usa CancellationToken para soportar detención segura del Worker.
    /// </summary>
    Task<IReadOnlyList<T>> ExtractAsync(CancellationToken ct = default);
}
