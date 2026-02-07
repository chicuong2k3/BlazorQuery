using BlazorStatic;

namespace SwrSharp.Doc.Components;

public class DocFrontMatter : IFrontMatter
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int Order { get; set; }
    public string Category { get; set; } = "";
}

