using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Syncfusion.Blazor;
using Syncfusion.Licensing;
using ManaApp;
using ManaApp.Services;
using ManaApp.Services.Planning;
using ManaApp.Services.Workflow;



var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped<GanttSimulationService>();
// HttpClient base adrese – kā līdz šim izmanto tiešos URL uz API
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("http://localhost:5270/")
});



SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JHaF1cWGhIfEx1RHxQdld5ZFRHallYTnNWUj0eQnxTdENjXH9YcXRRQGFUUUNyXUleYA==");    

// Syncfusion komponentēm (SfDialog u.c.)
builder.Services.AddSyncfusionBlazor();

builder.Services.AddScoped<ManaApp.Services.AppState>();

builder.Services.AddScoped<GanttChartService>();
builder.Services.AddScoped<PlanningLookupService>();
builder.Services.AddScoped<PlanningDataService>();
builder.Services.AddScoped<PlanningDraftService>();

builder.Services.AddScoped<WorkflowApiService>();
builder.Services.AddScoped<WorkflowStateService>();
builder.Services.AddScoped<WorkflowEditorService>();
builder.Services.AddScoped<TechnologyEditorService>();
builder.Services.AddScoped<TechnologyTreeBuilder>();

var culture = new System.Globalization.CultureInfo("lv-LV");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;

await builder.Build().RunAsync();


