using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

// ── CORS — allow the frontend to call this API ────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();
app.UseCors();

// ── Database config from environment variables ────────────────────────────
string DbHost     = Environment.GetEnvironmentVariable("DB_HOST")     ?? "mariadb";
string DbPort     = Environment.GetEnvironmentVariable("DB_PORT")     ?? "3306";
string DbUser     = Environment.GetEnvironmentVariable("DB_USER")     ?? "rpuser";
string DbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "rppassword123";
string DbName     = Environment.GetEnvironmentVariable("DB_NAME")     ?? "resourceplanner";

string ConnectionString =>
    $"Server={DbHost};Port={DbPort};Database={DbName};User={DbUser};Password={DbPassword};";

// ── Initialize database ───────────────────────────────────────────────────
async Task InitDb()
{
    int attempts = 0;
    while (attempts < 10)
    {
        try
        {
            await using var conn = new MySqlConnection(ConnectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();

            // Create table if it doesn't exist
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS app_data (
                    id      INT PRIMARY KEY DEFAULT 1,
                    payload LONGTEXT NOT NULL,
                    updated TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                )";
            await cmd.ExecuteNonQueryAsync();

            // Insert empty row if none exists
            cmd.CommandText = "INSERT IGNORE INTO app_data (id, payload) VALUES (1, '{}')";
            await cmd.ExecuteNonQueryAsync();

            Console.WriteLine("Database ready");
            return;
        }
        catch (Exception ex)
        {
            attempts++;
            Console.WriteLine($"DB connection attempt {attempts} failed: {ex.Message}");
            await Task.Delay(5000);
        }
    }
    Console.WriteLine("Could not connect to database after 10 attempts");
}

await InitDb();

// ── Health check ──────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// ── GET all data ──────────────────────────────────────────────────────────
app.MapGet("/api/data", async () =>
{
    try
    {
        await using var conn = new MySqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT payload FROM app_data WHERE id = 1";

        var result = await cmd.ExecuteScalarAsync();
        var json = result?.ToString() ?? "{}";

        return Results.Content(json, "application/json");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"GET /api/data error: {ex.Message}");
        return Results.Problem("Could not load data");
    }
});

// ── POST (save) all data ──────────────────────────────────────────────────
app.MapPost("/api/data", async (HttpRequest request) =>
{
    try
    {
        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync();

        await using var conn = new MySqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE app_data SET payload = @payload WHERE id = 1";
        cmd.Parameters.AddWithValue("@payload", payload);
        await cmd.ExecuteNonQueryAsync();

        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"POST /api/data error: {ex.Message}");
        return Results.Problem("Could not save data");
    }
});

app.Run($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");
