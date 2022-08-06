using libreoffice_test;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "localhost",
        corsPolicyBuilder =>
        {
            corsPolicyBuilder.WithOrigins("http://localhost:5001");
        });
});

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<ILibreofficeProcessService, LibreofficeProcessService>();
builder.Services.AddHostedService<LibreofficeProcessService>();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5001);
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("localhost");
    
app.UseAuthorization();

app.MapControllers();

app.Run();