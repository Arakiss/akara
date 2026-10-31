using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using ForjaHoradrica;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1024 * 1024);
string? portArg = null;
for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--port") portArg = args[i + 1];
int port = int.TryParse(portArg, out int requestedPort) && requestedPort is >= 0 and <= 65535 ? requestedPort : 0;
builder.WebHost.UseUrls("http://127.0.0.1:" + port);
builder.Services.ConfigureHttpJsonOptions(options => { options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; options.SerializerOptions.PropertyNameCaseInsensitive = true; options.SerializerOptions.MaxDepth = 32; });
string? dataDirectory = builder.Configuration["data-dir"];
builder.Services.AddSingleton(_ => new EditorService(dataDirectory));
var app = builder.Build();
string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
app.Use(async (context, next) =>
{
    try
    {
        if (context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote) || (context.Request.Host.Host != "127.0.0.1" && context.Request.Host.Host != "localhost")) { context.Response.StatusCode = 403; return; }
        if (context.Request.Headers.TryGetValue("Origin", out var origin) && origin.ToString() != $"http://{context.Request.Host}") { context.Response.StatusCode = 403; return; }
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            string supplied = context.Request.Headers["X-Forja-Token"].ToString();
            if (supplied.Length == 0 && context.Request.Path == "/api/bootstrap") supplied = context.Request.Query["token"].ToString();
            var a = System.Text.Encoding.UTF8.GetBytes(supplied); var b = System.Text.Encoding.UTF8.GetBytes(token);
            if (a.Length != b.Length || !CryptographicOperations.FixedTimeEquals(a, b)) throw new ApiProblem("unauthorized", "Esta sesión local no está autorizada. Abre AKARA desde su ejecutable.", 401);
            if (context.Request.Method is "POST" or "PUT" or "PATCH" && !context.Request.HasJsonContentType()) throw new ApiProblem("json_required", "La petición debe enviar JSON.", 415);
        }
        await next(context);
    }
    catch (ApiProblem problem) { context.Response.StatusCode = problem.Status; await context.Response.WriteAsJsonAsync(new { code = problem.Code, message = problem.Message, details = problem.Details }); }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or NotSupportedException or InvalidOperationException or JsonException or OverflowException)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { code = "operation_failed", message = "No se pudo completar la operación: " + ex.Message });
    }
    catch (Exception) { context.Response.StatusCode = 500; await context.Response.WriteAsJsonAsync(new { code = "internal_error", message = "La operación no se pudo completar. La partida original se conserva." }); }
});
app.MapGet("/api/bootstrap", (EditorService service) => service.Bootstrap());
app.MapGet("/api/profiles", (EditorService service) => service.Profiles());
app.MapPost("/api/profiles", (ProfileRequest r, EditorService service) => service.AddProfile(r));
app.MapPost("/api/open", (OpenRequest r, EditorService service) => service.Open(r));
app.MapPost("/api/create", (CreateRequest r, EditorService service) => service.Create(r));
app.MapPost("/api/clone", (CloneRequest r, EditorService service) => service.Clone(r));
app.MapGet("/api/classes", (string? profileId, EditorService service) => service.Classes(profileId));
app.MapGet("/api/presets", (string? profileId, EditorService service) => service.Presets(profileId));
app.MapGet("/api/world", (string sessionId, string? planId, EditorService service) => service.World(sessionId, planId));
app.MapGet("/api/blueprint", (string sessionId, string itemId, string? planId, EditorService service) => service.ExportBlueprint(sessionId, itemId, planId));
app.MapGet("/api/loadouts", (EditorService service) => service.Loadouts());
app.MapGet("/api/loadouts/{id}", (string id,EditorService service) => service.GetLoadout(id));
app.MapPost("/api/loadouts/export", (SaveLoadoutRequest r,EditorService service) => service.ExportLoadout(r.SessionId,r.Name,r.PlanId,r.IncludeCharacter));
app.MapPost("/api/loadouts/save", (SaveLoadoutRequest r,EditorService service) => service.SaveLoadout(r));
app.MapPost("/api/loadouts/import", (ImportLoadoutRequest r,EditorService service) => service.ImportLoadout(r));
app.MapDelete("/api/loadouts/{id}", (string id,EditorService service) => service.DeleteLoadout(id));
app.MapGet("/api/sessions/{id}", (string id, EditorService service) => service.InspectSession(id));
app.MapGet("/api/catalog", (string sessionId, string type, string? search, int? offset, int? limit, EditorService service) => service.Catalog(sessionId, type, search, offset ?? 0, limit ?? 100));
app.MapPost("/api/preview", (PreviewRequest r, EditorService service) => service.Preview(r));
app.MapPost("/api/export", (ExportRequest r, EditorService service) => service.Export(r));
app.MapPost("/api/commit", (CommitRequest r, EditorService service) => service.Commit(r));
app.MapGet("/api/backups", (string sessionId, EditorService service) => service.GetBackups(sessionId));
app.MapPost("/api/restore", (RestoreRequest r, EditorService service) => service.Restore(r));
app.MapPost("/api/shutdown", (HttpContext context, IHostApplicationLifetime lifetime) =>
{
    context.Response.OnCompleted(() => { lifetime.StopApplication(); return Task.CompletedTask; });
    return Results.Ok(new { closed = true });
});

var assembly = typeof(Program).Assembly;
var embedded = assembly.GetManifestResourceNames().Where(n => n.Contains(".wwwroot.")).ToDictionary(n => n[(n.IndexOf(".wwwroot.", StringComparison.Ordinal) + 9)..], n => n);
app.MapFallback("/{**path}", async context =>
{
    if (context.Request.Path.StartsWithSegments("/api")) { context.Response.StatusCode = 404; return; }
    string path = context.Request.Path.Value?.TrimStart('/') ?? "";
    string resourceKey = path.Replace('/', '.');
    if (!embedded.TryGetValue(resourceKey, out var resource))
    {
        if (Path.HasExtension(path)) { context.Response.StatusCode = 404; return; }
        embedded.TryGetValue("index.html", out resource);
    }
    if (resource is not null)
    {
        context.Response.ContentType = Path.GetExtension(path) switch { ".js" => "text/javascript; charset=utf-8", ".css" => "text/css; charset=utf-8", ".svg" => "image/svg+xml", ".png" => "image/png", ".woff2" => "font/woff2", _ => "text/html; charset=utf-8" };
        using var stream = assembly.GetManifestResourceStream(resource)!; await stream.CopyToAsync(context.Response.Body); return;
    }
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync("<!doctype html><html lang=es><meta charset=utf-8><title>AKARA</title><body style='font-family:system-ui;background:#13171a;color:#f4e8ca;padding:48px'><h1>AKARA</h1><p>El núcleo local está activo. Compila e incorpora la interfaz para abrir el editor.</p></body></html>");
});
await app.StartAsync();
string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
string url = address + "/?token=" + token;
Console.WriteLine("AKARA_READY " + url);
if (!args.Contains("--no-browser")) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
await app.WaitForShutdownAsync();
