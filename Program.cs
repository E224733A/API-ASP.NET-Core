using API_ASP.NET_Core.Data;
using API_ASP.NET_Core.Mappers;
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
        // Le contrat JSON v1.2 doit rester tolérant sur la casse des propriétés reçues.
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;

        // Important pour le contrat JSON v1.2 :
        // les champs null doivent apparaître dans les réponses.
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
        Version = "v1.2",
        Description = """
        API ASP.NET Core utilisée par l'application mobile MobileSLI des livreurs.

        Contrat JSON actuel : schemaVersion = "1.2".

        L'API permet :
        - de vérifier l'état technique de l'API ;
        - de consulter les livreurs ;
        - de lister les tournées disponibles pour une date et un livreur ;
        - de charger le détail complet d'une tournée avant le départ ;
        - de recevoir la synchronisation finale envoyée par le mobile en fin de journée ;
        - de consulter les synchronisations enregistrées côté administration.

        Principe d'architecture :
        - le mobile ne se connecte jamais directement à SQL Server ;
        - le mobile échange uniquement avec cette API en HTTP/JSON ou HTTPS/JSON ;
        - les vues ABSSolute restent la source de lecture métier ;
        - les tables Mobile_* stockent les synchronisations, les lignes, les quantités et les logs.

        Champs importants du contrat v1.2 :
        - saisie.quantites[] remplace les anciennes colonnes fixes NbRolls / NbTapis / NbSacs ;
        - quantiteLivreePrevue est optionnel et nullable ;
        - commentaireExceptionnel permet d'afficher une remarque ponctuelle saisie côté administration ou expédition ;
        - zoneDechargementAffichee permet d'afficher directement la zone de déchargement calculée si l'API la fournit.
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
builder.Services.AddScoped<SynchronisationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle = "API Mobile SLI - Swagger";
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "API Mobile SLI v1.2");
        options.DisplayRequestDuration();
    });
}

// En développement local, on laisse HTTP.
// En production sous IIS, HTTPS sera configuré proprement.
// app.UseHttpsRedirection();

app.MapControllers();

app.Run();