using API_ASP.NET_Core.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// OpenAPI + Swagger UI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Connexion SQL Server
builder.Services.AddSingleton<SqlConnectionFactory>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Désactivé pour le développement local HTTP.
// En production, HTTPS sera configuré correctement sous IIS.
 // app.UseHttpsRedirection();

app.MapControllers();

app.Run();