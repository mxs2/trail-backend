using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using System.Reflection;
using Trail.Api.Extensions;
using Trail.Api.Infrastructure.Data;
using Trail.Api.Swagger;

var builder = WebApplication.CreateBuilder(args);

// Accept enum values as strings ("Approved") OR integers (0) in JSON bodies.
// Without this, System.Text.Json only parses integer enums by default, so
// sending { "decision": "Approved" } from the frontend returns a 400.
builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Trail API", Version = "v1" });
    // include XML comments (enable in csproj: GenerateDocumentationFile)
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste only the JWT token here"
    });

    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", null, null),
            []
        }
    });

    options.OperationFilter<SwaggerBearerAuthOperationFilter>();
});
builder.Services.AddProblemDetails();

builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddApplicationServices();
builder.Services.AddAiServices(builder.Configuration);

// GitHub API proxy — sets User-Agent (required by GitHub) and base URL
builder.Services.AddHttpClient("github", client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Trail-Platform/1.0");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
});

var app = builder.Build();

app.UseSwagger(options =>
{
    options.RouteTemplate = "openapi/{documentName}.json";
});
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Trail API v1");
    options.RoutePrefix = "swagger";
});
app.MapGet("/", () => Results.Redirect("/swagger"))
    .ExcludeFromDescription();

// No Railway, o SSL é terminado no Proxy. 
// O UseHttpsRedirection pode atrapalhar o healthcheck interno.
// if (!app.Environment.IsDevelopment())
//     app.UseHttpsRedirection();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Tenta rodar migrações, mas garante que a porta seja aberta
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting app on port {Port}", port);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var dbLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try 
    {
        dbLogger.LogInformation("Attempting to run database migrations...");
        await db.Database.MigrateAsync();
        dbLogger.LogInformation("Migrations completed successfully.");
        await DbSeeder.SeedAsync(db, dbLogger);
    }
    catch (Exception ex)
    {
        dbLogger.LogCritical(ex, "FATAL: Could not connect or migrate database. Check connection string and Firewall.");
        // Não deixamos travar aqui para que o healthcheck possa ao menos responder algo ou o log aparecer
    }
}

app.Run();
