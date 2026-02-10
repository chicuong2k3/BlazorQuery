using BlazorStatic;
using SwrSharp.Doc.Components;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

builder.Services.AddBlazorStaticService(opt =>
        {
            opt.ShouldGenerateSitemap = true;
            opt.SiteUrl = "https://chicuong2k3.github.io/BlazorQuery";
            opt.OutputFolderPath = "output"; // Explicitly set output folder
        }
    )
    // Docs content service
    .AddBlazorStaticContentService<DocFrontMatter>(opt =>
    {
        opt.PageUrl = "docs";
        opt.ContentPath = "Content/Docs";
    });

builder.Services.AddRazorComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>();

// UseBlazorStaticGenerator must be called in all environments so that
// ParseAndAddPosts() runs and ContentService.Posts gets populated.
// In Development, shutdownApp: false keeps the server running.
app.UseBlazorStaticGenerator(shutdownApp: !app.Environment.IsDevelopment());

app.Run();

public static class WebsiteKeys
{
    public const string GitHubRepo = "https://github.com/chicuong2k3/BlazorQuery";
    public const string Title = "SwrSharp Documentation";
}