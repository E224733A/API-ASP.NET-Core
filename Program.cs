using API_ASP.NET_Core.Data;
using API_ASP.NET_Core.Mappers;
using API_ASP.NET_Core.Middleware;
using API_ASP.NET_Core.Repositories;
using API_ASP.NET_Core.Services;
using API_ASP.NET_Core.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Le contrat JSON doit rester tolérant sur la casse des propriétés reçues.
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;

        // Important : les champs null doivent apparaître dans les réponses.
        // Exemple : quantiteLivreePrevue = null signifie que l'expédition n'a rien renseigné.
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                ? "La requête JSON est invalide."
                : e.ErrorMessage)
            .ToArray();

        return new BadRequestObjectResult(new
        {
            statut = "VALIDATION_ERROR",
            errors
        });
    };
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API Mobile SLI - Tournées livreurs",
        Version = "v1.3",
        Description = """
        API ASP.NET Core utilisée par l'application mobile MobileSLI des livreurs
        et par le module web Expédition.

        Contrat JSON mobile actuel : schemaVersion = "1.2".
        Contrat JSON Expédition actuel : schemaVersion = "1.2".

        Règle centrale de date métier :
        - la date autorisée est calculée côté API avec le fuseau Europe/Paris ;
        - sur Windows Server, l'identifiant de fuseau utilisé est Romance Standard Time ;
        - le fallback Europe/Paris permet aussi l'exécution sur Linux ou en environnement de développement ;
        - les POST d'écriture refusent toute date différente de la date serveur autorisée ;
        - un POST ancien ou futur retourne 409 Conflict ;
        - les GET mobiles ne doivent pas accepter de date envoyée par le client.

        Routes Expédition finales :
        - GET  /api/expedition/preparations/a-preparer
        - POST /api/expedition/preparations/verrouiller

        Le GET Expédition est global :
        - aucun paramètre dateTournee ;
        - aucun paramètre codeTournee ;
        - aucun paramètre codeLivreur ;
        - la date préparable est calculée côté API ;
        - la sélection de tournée reste côté application web Expédition.

        Le POST Expédition est global :
        - idLotVerrouillage obligatoire ;
        - source = APPLICATION_WEB_EXPEDITION ;
        - fuseauHoraireMetier = Europe/Paris ;
        - dateVerrouillageDemandee avec offset ISO 8601 ;
        - corps JSON avec tournees[] / lignes[] / quantitesPrevues[] ;
        - dateTournee doit correspondre à la date métier autorisée côté API.

        Principe d'architecture :
        - le mobile ne se connecte jamais directement à SQL Server ;
        - le navigateur Expédition ne se connecte jamais directement à SQL Server ;
        - le module web Expédition utilise SQLite local pour ses brouillons avant verrouillage ;
        - l'API reste responsable du contrôle final avant sauvegarde définitive ;
        - les vues ABSSolute restent la source de lecture métier ;
        - les tables Mobile_* stockent les synchronisations, les pré-remplissages verrouillés et les logs.
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
builder.Services.AddSingleton<DateMetierService>();

builder.Services.AddScoped<LivreursRepository>();

// Service métier pour les livreurs. Il encapsule l'accès au dépôt et allège le contrôleur.
builder.Services.AddScoped<LivreursService>();

builder.Services.AddScoped<TourneesRepository>();
builder.Services.AddScoped<TourneeRequestValidator>();
builder.Services.AddScoped<TourneesService>();
builder.Services.AddScoped<TourneeMobileMapper>();

builder.Services.AddScoped<SynchronisationsRepository>();
builder.Services.AddScoped<SynchronisationTourneeValidator>();
builder.Services.AddScoped<SynchronisationMapper>();
builder.Services.AddScoped<SynchronisationService>();

// Module Expédition : 2 routes API seulement.
// GET /api/expedition/preparations/a-preparer
// POST /api/expedition/preparations/verrouiller
builder.Services.AddScoped<ExpeditionRepository>();
builder.Services.AddScoped<ExpeditionService>();

// Services supplémentaires pour respecter l’architecture MVC :
// - service de préparation et de verrouillage qui délèguent au service existant ;
// - validator et mapper dédiés au module Expédition.
builder.Services.AddScoped<ExpeditionPreparationService>();
builder.Services.AddScoped<ExpeditionVerrouillageService>();
builder.Services.AddScoped<ExpeditionVerrouillageValidator>();
builder.Services.AddScoped<ExpeditionMapper>();

var app = builder.Build();

// Middlewares globaux : traçabilité et gestion d'erreurs
app.UseCorrelationId();
app.UseApiExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle = "API Mobile SLI - Swagger";
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "API Mobile SLI v1.3");
        options.DisplayRequestDuration();
    });
}

// En développement local, on laisse HTTP.
// En production sous IIS, HTTPS sera configuré proprement.
// app.UseHttpsRedirection();

app.MapControllers();

app.Run();