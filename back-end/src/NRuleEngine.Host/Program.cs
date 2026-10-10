using NRuleEngine.Host;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseAutofac();
builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
await builder.AddApplicationAsync<NRuleEngineHostModule>();

var app = builder.Build();
await app.InitializeApplicationAsync();
await app.InitializeDatabaseAsync();

app.UseRouting();
app.UseCors(NRuleEngineHostModule.CorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
