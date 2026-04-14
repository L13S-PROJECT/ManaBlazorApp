using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Syncfusion.Blazor;
using Syncfusion.Licensing;
using ManaApp;
using ManaApp.Services;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped<GanttSimulationService>();
// HttpClient base adrese – kā līdz šim izmanto tiešos URL uz API
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("http://localhost:5270/")
});



SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjGyl/Vkd+XU9FcVRDX3xKf0x/TGpQb19xflBPallYVBYiSV9jS3tSdkVkWH1ceHZXQWlcWU91Xg==");    

// Syncfusion komponentēm (SfDialog u.c.)
builder.Services.AddSyncfusionBlazor();

builder.Services.AddScoped<ManaApp.Services.AppState>();

builder.Services.AddScoped<GanttChartService>();

var culture = new System.Globalization.CultureInfo("lv-LV");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;

await builder.Build().RunAsync();
