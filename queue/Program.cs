using SmartQueueSystemGUI.Services;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = Environment.GetEnvironmentVariable("CAMPUS_PUBLIC_HTTPS") == "1" ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddHttpClient("campus").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseProxy = false, UseCookies = false });
builder.Services.AddSingleton<QueueStore>();
builder.Services.AddSingleton<SchoolAssistantService>();
builder.Services.AddSingleton<VisitEligibilityService>();
builder.Services.AddSingleton<AssistantThrottle>();
builder.Services.AddHttpClient("school-ai",c => c.Timeout=TimeSpan.FromSeconds(20));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = Environment.GetEnvironmentVariable("CAMPUS_PUBLIC_HTTPS") == "1" ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".SmartQueueSystemGUI.Session";
});

var app = builder.Build();
app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto });

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}


app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseMiddleware<CampusSession>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();
