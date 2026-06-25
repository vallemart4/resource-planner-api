using MySqlConnector;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseCors();

// Config from environment
var dbHost     = Environment.GetEnvironmentVariable("DB_HOST")     ?? "mariadb";
var dbPort     = Environment.GetEnvironmentVariable("DB_PORT")     ?? "3306";
var dbUser     = Environment.GetEnvironmentVariable("DB_USER")     ?? "rpuser";
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "rppassword123";
var dbName     = Environment.GetEnvironmentVariable("DB_NAME")     ?? "resourceplanner";
var connStr    = $"Server={dbHost};Port={dbPort};Database={dbName};User={dbUser};Password={dbPassword};";

// Init DB
for (int i = 0; i < 10; i++)
{
    try
    {
        await using var conn = new MySqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS app_data (
            id INT PRIMARY KEY DEFAULT 1,
            payload LONGTEXT NOT NULL,
            updated TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP)";
        await cmd.ExecuteNonQueryAsync();
        cmd.CommandText = "INSERT IGNORE INTO app_data (id, payload) VALUES (1, '{}')";
        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine("Database ready");
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DB attempt {i+1} failed: {ex.Message}");
        await Task.Delay(5000);
    }
}

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// GET data
app.MapGet("/api/data", async () =>
{
    try
    {
        await using var conn = new MySqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT payload FROM app_data WHERE id = 1";
        var result = await cmd.ExecuteScalarAsync();
        return Results.Content(result?.ToString() ?? "{}", "application/json");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"GET error: {ex.Message}");
        return Results.Problem("Could not load data");
    }
});

// POST data
app.MapPost("/api/data", async (HttpRequest request) =>
{
    try
    {
        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync();
        await using var conn = new MySqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE app_data SET payload = @payload WHERE id = 1";
        cmd.Parameters.AddWithValue("@payload", payload);
        await cmd.ExecuteNonQueryAsync();
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"POST error: {ex.Message}");
        return Results.Problem("Could not save data");
    }
});

app.Run($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");
