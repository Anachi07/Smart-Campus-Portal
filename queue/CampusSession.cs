using System.Net.Http.Json;
using System.Text.Json;
using SmartQueueSystemGUI.Services;

public record CampusIdentity(int Id, string FullName, string Role, string Phone);
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
            context.Session.SetString("PhoneNumber", user.PhoneNumber);
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
