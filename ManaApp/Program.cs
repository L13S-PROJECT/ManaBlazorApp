using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Syncfusion.Blazor;
using Syncfusion.Licensing;
using ManaApp;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient base adrese – kā līdz šim izmanto tiešos URL uz API
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("http://localhost:5270/")
});



SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjGyl/Vkd+XU9FcVRDX3xKf0x/TGpQb19xflBPallYVBYiSV9jS3tSdkVkWH1ceHZXQWlcWU91Xg==");    

// Syncfusion komponentēm (SfDialog u.c.)
builder.Services.AddSyncfusionBlazor();

builder.Services.AddScoped<ManaApp.Services.AppState>();

await builder.Build().RunAsync();
