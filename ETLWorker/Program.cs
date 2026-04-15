using ETLWorker;
using ETLWorker.Abstractions;
using ETLWorker.Extractors;
using ETLWorker.Loaders;
using ETLWorker.Models;
using ETLWorker.Services;
using ETLWorker.Staging;
using Microsoft.Extensions.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

// Opciones de configuracion
builder.Services.Configure<CsvExtractorOptions>(
    builder.Configuration.GetSection("Sources:Csv"));

builder.Services.Configure<DatabaseExtractorOptions>(
    builder.Configuration.GetSection("Sources:Database"));

builder.Services.Configure<ApiExtractorOptions>(
    builder.Configuration.GetSection("Sources:Api"));

builder.Services.Configure<StagingOptions>(
    builder.Configuration.GetSection("Staging"));

builder.Services.Configure<DimensionLoaderOptions>(opts =>
{
    opts.ConnectionString = builder.Configuration.GetConnectionString("MySQL") ?? "";
    opts.StagingDirectory = builder.Configuration["Staging:Directory"] ?? "staging";
});

// CsvExtractor
builder.Services.AddTransient<CsvExtractor>();
builder.Services.AddTransient<IExtractor<Cliente>>(
    sp => sp.GetRequiredService<CsvExtractor>());
builder.Services.AddTransient<IExtractor<Fuente>>(
    sp => sp.GetRequiredService<CsvExtractor>());
builder.Services.AddTransient<IExtractor<Producto>>(
    sp => sp.GetRequiredService<CsvExtractor>());
builder.Services.AddTransient<IExtractor<Encuesta>>(
    sp => sp.GetRequiredService<CsvExtractor>());
builder.Services.AddTransient<IExtractor<ComentarioSocial>>(
    sp => sp.GetRequiredService<CsvExtractor>());

// DatabaseExtractor
builder.Services.AddTransient<IExtractor<ReviewWeb>, DatabaseExtractor>();

// ApiExtractor
builder.Services.AddTransient<ApiExtractor>();
builder.Services.AddHttpClient("ApiExtractor", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Staging y Extraccion
builder.Services.AddTransient<StagingService>();
builder.Services.AddTransient<ExtractionService>();

// Loaders de Dimensiones
builder.Services.AddTransient<IDimensionLoader, ClientesLoader>();
builder.Services.AddTransient<IDimensionLoader, ProductosLoader>();
builder.Services.AddTransient<IDimensionLoader, FuenteLoader>();
builder.Services.AddTransient<IDimensionLoader, EncuestaLoader>();
builder.Services.AddTransient<IDimensionLoader, ComentariosSocialesLoader>();
builder.Services.AddTransient<IDimensionLoader, ReviewWebLoader>();
builder.Services.AddTransient<LoadingService>();

// Worker
builder.Services.AddHostedService<EtlWorker>();

var host = builder.Build();
host.Run();
