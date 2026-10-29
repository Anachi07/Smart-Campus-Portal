using System.Net.Http.Json;
using System.Text.Json;
using SmartQueueSystemGUI.Services;
using SmartQueueSystemGUI.Models;

public record CampusIdentity(int Id, string FullName, string Role, string Phone, string Csrf, string StudentNumber, List<CampusClass>? Classes);
public class CampusSession
{
    private readonly RequestDelegate _next;
    public CampusSession(RequestDelegate next) => _next = next;
    public async Task InvokeAsync(HttpContext context, QueueStore store, IHttpClientFactory clients)
    {
        var path = context.Request.Path.Value ?? "/";
        if (path.StartsWith("/Auth", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect(path.Contains("Logout", StringComparison.OrdinalIgnoreCase)
                ? "/sims/logout.php" : path.Contains("Register", StringComparison.OrdinalIgnoreCase)
                ? "/sims/register.php" : "/sims/login.php");
            return;
        }
        if (path.Equals("/internal/queue-reload", StringComparison.OrdinalIgnoreCase)) {
            var expected = Environment.GetEnvironmentVariable("CAMPUS_INTERNAL_SECRET") ?? "";
            var supplied = context.Request.Headers["X-Campus-Secret"].ToString();
            if (context.Request.Method != "POST" || expected.Length == 0 || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(expected), System.Text.Encoding.UTF8.GetBytes(supplied))) { context.Response.StatusCode=404; return; }
            try {
                if (context.Request.Query["validate"] == "1") {
                    using var reader = new StreamReader(context.Request.Body);
                    QueueStore.ValidateBackup(await reader.ReadToEndAsync());
                } else store.Reload();
                context.Response.StatusCode=204;
            } catch (Exception error) when (error is JsonException or InvalidDataException) { context.Response.StatusCode=400; }
            return;
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:8082/campus-session.php");
        request.Headers.Add("X-Campus-Secret", Environment.GetEnvironmentVariable("CAMPUS_INTERNAL_SECRET"));
        if (context.Request.Headers.TryGetValue("Cookie", out var cookie)) request.Headers.Add("Cookie", cookie.ToString());
        CampusIdentity? identity = null;
        try
        {
            using var response = await clients.CreateClient("campus").SendAsync(request);
            if (response.IsSuccessStatusCode) identity = await response.Content.ReadFromJsonAsync<CampusIdentity>();
            else if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            { context.Session.Clear(); context.Response.StatusCode = 503; await context.Response.WriteAsync("Campus login service is unavailable."); return; }
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException) { context.Session.Clear(); context.Response.StatusCode = 503; await context.Response.WriteAsync("Campus login service is unavailable."); return; }
        if (identity == null) context.Session.Clear();
        else
        {
            var user = store.CampusUser(identity.Id, identity.FullName, identity.Role, identity.Phone);
            context.Session.SetInt32("UserId", user.Id);
            context.Session.SetString("FullName", user.FullName);
            context.Session.SetString("Role", user.Role);
            context.Session.SetString("CampusRole", identity.Role);
            context.Session.SetString("PhoneNumber", user.PhoneNumber);
            context.Session.SetString("CampusCsrf", identity.Csrf);
            context.Session.SetString("StudentNumber", identity.StudentNumber);
            context.Session.SetString("CampusClasses", JsonSerializer.Serialize(identity.Classes ?? new()));
        }
        if (path == "/") { context.Response.Redirect("/sims/dashboard.php"); return; }
        // Identity and role management belongs to SIMS; queue-local passwords cannot bypass it.
        if (path.StartsWith("/Admin/Users", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/Admin/AddUser", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/Admin/DeleteUser", StringComparison.OrdinalIgnoreCase))
        { context.Response.Redirect("/sims/modules/admin/manage.php?entity=users"); return; }
        await _next(context);
    }
}
