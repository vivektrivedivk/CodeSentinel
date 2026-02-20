using CodeSentinel.API.Services;

namespace CodeSentinel.API.Infrastructure;

internal static class DatabaseInitializer
{
    internal static async Task InitializeAsync(VectorStore store)
    {
        await store.EnsureSchemaAsync();
    }
}
