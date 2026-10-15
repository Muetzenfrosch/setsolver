using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SetGameSolver.Web;
using SetGameSolver.Web.Localization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddSingleton<LanguageState>();

var host = builder.Build();
// Sprache vor dem ersten Zeichnen bestimmen, damit die Seite nicht kurz in der falschen Sprache erscheint.
await host.Services.GetRequiredService<LanguageState>().InitializeAsync();
await host.RunAsync();
