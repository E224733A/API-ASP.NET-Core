using API_ASP.NET_Core.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<SqlConnectionFactory>();

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