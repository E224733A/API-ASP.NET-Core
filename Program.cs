using API_ASP.NET_Core.Data;
using API_ASP.NET_Core.Repositories;
using API_ASP.NET_Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<SqlConnectionFactory>();

builder.Services.AddScoped<LivreursRepository>();

builder.Services.AddScoped<TourneesRepository>();
builder.Services.AddScoped<TourneesService>();

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