var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorPages();
builder.Services.AddHttpClient("graph", client => client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/"));
builder.Services.AddHttpClient("calendar-api", client => client.BaseAddress = new Uri("https+http://calendar-api"));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapDefaultEndpoints();

app.Run();
