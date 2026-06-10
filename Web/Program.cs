using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Web;
using Web.Services;
using Web.Authentication;
using Web.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authorization;
using Blazored.LocalStorage;
using System.Globalization;
using System.Net.Http;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Web.Components.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Load configuration from wwwroot/appsettings.json if present.
// We use GetByteArrayAsync because synchronous reads on a streaming response
// are not supported by the browser HttpClient in newer WASM runtimes.
try
{
    var configClient = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
    var configBytes = await configClient.GetByteArrayAsync("appsettings.json");
    using var configStream = new MemoryStream(configBytes);
    builder.Configuration.AddJsonStream(configStream);
}
catch (Exception ex)
{
    Console.WriteLine($"Could not load wwwroot/appsettings.json: {ex.Message}");
}

// Register HttpClient with an authorization handler that reads token from local storage
builder.Services.AddTransient<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient("ApiClient", client =>
{
    var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
    client.BaseAddress = new Uri(apiBaseUrl);
})
.AddHttpMessageHandler<AuthorizationHeaderHandler>();

// Add HttpClient for localization files (without auth header)
builder.Services.AddHttpClient("LocalizationClient", client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
});

// Provide IHttpClientFactory-created client for DI (used by services)
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("ApiClient"));

// JSON-based Localization
builder.Services.AddSingleton<JsonStringLocalizerFactory>();
builder.Services.AddScoped<IJsonStringLocalizer>(sp =>
{
    var factory = sp.GetRequiredService<JsonStringLocalizerFactory>();
    return factory.CreateSync();
});

// Add authentication and authorization
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

// Register Blazored LocalStorage
builder.Services.AddBlazoredLocalStorage();

// Add custom authorization
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddAuthorizationCore();

// Add management services
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();
builder.Services.AddScoped<IPermissionManagementService, PermissionManagementService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Register client-side implementations of services
builder.Services.AddScoped<IUserLogService, ClientUserLogService>();
builder.Services.AddScoped<IMediaService, ClientMediaService>();
builder.Services.AddScoped<ILookupService, ClientLookupService>();
builder.Services.AddScoped<IAssistantAdminService, ClientAssistantAdminService>();
builder.Services.AddScoped<IDashboardClientService, ClientDashboardService>();
builder.Services.AddScoped<IAssistantClientService, ClientAssistantService>();
builder.Services.AddScoped<IAppsClientService, AppsClientService>();
builder.Services.AddScoped<INotificationClientService, NotificationClientService>();

// Register other services
// Note: We do NOT call AddApplication() or AddInfrastructure() as they are server-side.
// We need to register any validators manually if needed, or rely on API validation.

var host = builder.Build();

try 
{
    var js = host.Services.GetRequiredService<IJSRuntime>();
    var cultureName = await js.InvokeAsync<string>("AppUtils.initializeCulture");
    
    if (!string.IsNullOrEmpty(cultureName))
    {
        var culture = new CultureInfo(cultureName);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
    
    // Preload localizations
    var localizerFactory = host.Services.GetRequiredService<JsonStringLocalizerFactory>();
    await localizerFactory.CreateAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Initialization error: {ex.Message}");
}

await host.RunAsync();
