using API_ASP.NET_Core.Data;
using API_ASP.NET_Core.Mappers;
using API_ASP.NET_Core.Repositories;
using API_ASP.NET_Core.Services;
using API_ASP.NET_Core.Validators;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState.Values.SelectMany(v => v.Errors);

        return new BadRequestObjectResult(new
        {
            statut = "VALIDATION_ERROR",
            errors = errors.Select(e => e.ErrorMessage)
        });
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API Mobile SLI - Tournées livreurs",
        Version = "v1",
        Description = """
        API ASP.NET Core utilisée par l'application mobile des livreurs.

        Elle permet :
        - de consulter les tournées disponibles ;
        - de charger le détail d'une tournée ;
        - de recevoir les synchronisations envoyées par l'application mobile en fin de journée.

        L'API expose uniquement un contrat de données et d'échange.
        Les règles d'affichage propres à l'interface mobile sont gérées côté application mobile.
        """
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddSingleton<SqlConnectionFactory>();

builder.Services.AddScoped<LivreursRepository>();

builder.Services.AddScoped<TourneesRepository>();
builder.Services.AddScoped<TourneesService>();
builder.Services.AddScoped<TourneeMobileMapper>();

builder.Services.AddScoped<SynchronisationsRepository>();
builder.Services.AddScoped<SynchronisationTourneeValidator>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// En développement local, on laisse HTTP.
// En production sous IIS, HTTPS sera configuré proprement.
// app.UseHttpsRedirection();

app.MapControllers();

app.Run();