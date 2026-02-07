using BlazorStatic;
using SwrSharp.Doc.Components;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

builder.Services.AddBlazorStaticService(opt =>
        {
            opt.ShouldGenerateSitemap = true;
            opt.SiteUrl = "https://swrsharp.dev";
            // opt.IgnoredPathsOnContentCopy.Add("file-in-wwwroot-that-i-dont-want");
        }
    )
    // Docs content service
    .AddBlazorStaticContentService<DocFrontMatter>(opt =>
    {
        opt.PageUrl = "docs";
        opt.ContentPath = "Content/Docs";
    })
    // Blog content service
    .AddBlazorStaticContentService<BlogFrontMatter>(opt =>
    {
        opt.PageUrl = "blog";
        opt.ContentPath = "Content/Blog";
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

// Only generate static site in production
if (!app.Environment.IsDevelopment())
{
    app.UseBlazorStaticGenerator(shutdownApp: true);
}

app.Run();

public static class WebsiteKeys
{
    public const string GitHubRepo = "https://github.com/BlazorStatic/SwrSharp.Doc";
    public const string X = "https://x.com/";
    public const string Title = "BlazorStatic Minimal Blog";
    public const string BlogPostStorageAddress = $"{GitHubRepo}/tree/main/Content/Blog";
    public const string BlogLead = "Sample blog created with BlazorStatic and TailwindCSS";
}