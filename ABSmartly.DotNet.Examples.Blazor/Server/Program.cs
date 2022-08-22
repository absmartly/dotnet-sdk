using ABSmartlySdk.Temp;
using ABSmartlySdk.Utils.Extensions;

var builder = WebApplication.CreateBuilder(args);
//var configuration = builder.Configuration;

// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var section = builder.Configuration.GetSection("ABSmartlyConfig");
builder.Services.Configure<ClientConfiguration>(section);
builder.Services.AddABSmartly(lifeTime: ServiceLifetime.Transient, options: config =>
{
    
});
//builder.Services.AddABSmartly(
//    config =>
//    {

//    }
//);
//builder.Services.AddABSmartly(configuration.GetSection(""));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();


app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
