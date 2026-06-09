using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Web;
using Web.Services;
using Web.Authentication;
using Web.Authorization;
using Web.Offline;
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
// Offline-aware wrapper around the cashier management service. Without it,
// StartShiftModal's GetCurrentCashierProfileAsync/SwitchMyStoreAsync calls
// fail when offline, or 400 when the server still sees a not-yet-replayed
// active shift.
builder.Services.AddScoped<CashierManagementService>();
builder.Services.AddScoped<ICashierManagementService>(sp => new OfflineCashierManagementService(
    sp.GetRequiredService<CashierManagementService>(),
    sp.GetRequiredService<IIndexedDbService>(),
    sp.GetRequiredService<ActiveStoreContext>(),
    sp.GetRequiredService<OfflineNetworkMonitor>()));
builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();
builder.Services.AddScoped<IPermissionManagementService, PermissionManagementService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Register client-side implementations of services
builder.Services.AddScoped<IUserLogService, ClientUserLogService>();
builder.Services.AddScoped<IMediaService, ClientMediaService>();
builder.Services.AddScoped<ILookupService, ClientLookupService>();
builder.Services.AddScoped<ICategoryService, ClientCategoryService>();

// The cashier panel needs offline-aware decorators around three core services.
// Concrete online implementations stay registered so the wrappers can delegate
// when no local cache exists (or when the active store hasn't been picked).
builder.Services.AddScoped<ClientProductService>();
builder.Services.AddScoped<OrderClientService>();
builder.Services.AddScoped<ShiftService>();
builder.Services.AddScoped<IProductService>(sp => new OfflineProductService(
    sp.GetRequiredService<ClientProductService>(),
    sp.GetRequiredService<IIndexedDbService>(),
    sp.GetRequiredService<ActiveStoreContext>(),
    sp.GetRequiredService<OfflineNetworkMonitor>()));
builder.Services.AddScoped<IOrderService>(sp => new OfflineOrderService(
    sp.GetRequiredService<OrderClientService>(),
    sp.GetRequiredService<IIndexedDbService>(),
    sp.GetRequiredService<ActiveStoreContext>(),
    sp.GetRequiredService<OfflineNetworkMonitor>(),
    sp.GetRequiredService<IOfflineSyncService>()));
builder.Services.AddScoped<IShiftService>(sp => new OfflineShiftService(
    sp.GetRequiredService<ShiftService>(),
    sp.GetRequiredService<IIndexedDbService>(),
    sp.GetRequiredService<ActiveStoreContext>(),
    sp.GetRequiredService<OfflineNetworkMonitor>(),
    sp.GetRequiredService<IOfflineSyncService>()));

builder.Services.AddScoped<ISupplierService, ClientSupplierService>();
builder.Services.AddScoped<IAssistantAdminService, ClientAssistantAdminService>();
builder.Services.AddScoped<IPromotionService, ClientPromotionService>();
builder.Services.AddScoped<IRequestService, ClientRequestService>();
builder.Services.AddScoped<IBranchService, ClientBranchService>();

// Branch Panel: in-memory active-branch context. Per spec, this is not
// persisted — a hard reload clears it and forces the user back to the
// branch selector. Branch pages call the same client services as Admin
// (IOrderService, IGoodsReceivingClientService, …) with a branchId arg.
builder.Services.AddScoped<ActiveBranchContext>();
// Cashier-facing wrapper reads from the cached `stores` object store when the
// inner service would 403 (cashier lacks warehouses.read). Admin requests with
// an empty cache fall through to the inner service unchanged.
builder.Services.AddScoped<ClientWarehouseService>();
builder.Services.AddScoped<IWarehouseService>(sp => new OfflineWarehouseService(
    sp.GetRequiredService<ClientWarehouseService>(),
    sp.GetRequiredService<IIndexedDbService>(),
    sp.GetRequiredService<OfflineNetworkMonitor>()));
builder.Services.AddScoped<ITerminalService, ClientTerminalService>();
builder.Services.AddScoped<IGoodsReceivingClientService, ClientGoodsReceivingService>();
builder.Services.AddScoped<IStockAdjustmentClientService, ClientStockAdjustmentService>();
builder.Services.AddScoped<IStockTransferClientService, ClientStockTransferService>();
builder.Services.AddScoped<IPurchaseRequestClientService, ClientPurchaseRequestService>();
builder.Services.AddScoped<IStocktakeClientService, ClientStocktakeService>();
builder.Services.AddScoped<IInventoryHistoryClientService, ClientInventoryHistoryService>();
builder.Services.AddScoped<IDashboardClientService, ClientDashboardService>();
builder.Services.AddScoped<IAssistantClientService, ClientAssistantService>();
builder.Services.AddScoped<IUnitService, ClientUnitService>();
builder.Services.AddScoped<INotificationClientService, NotificationClientService>();

// Cashier offline layer: IndexedDB, image cache, sync, and the active-store
// context. In Blazor WASM, Scoped == Singleton lifetime-wise (one app per
// tab) — Scoped is required here because ActiveStoreContext depends on the
// scoped ILocalStorageService for persisting the cashier's switched store
// across hard reloads.
builder.Services.AddScoped<ActiveStoreContext>();
builder.Services.AddScoped<IIndexedDbService, IndexedDbService>();
builder.Services.AddScoped<IImageCacheService, ImageCacheService>();
builder.Services.AddScoped<IOfflineSyncService, OfflineSyncService>();
builder.Services.AddScoped<OfflineNetworkMonitor>();

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
