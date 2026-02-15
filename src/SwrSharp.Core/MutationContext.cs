namespace SwrSharp.Core;

public class MutationContext(QueryClient client)
{
    public QueryClient Client { get; } = client;
}
