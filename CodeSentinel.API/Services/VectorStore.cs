using CodeSentinel.API.Models;
using CodeSentinel.API.Utilities;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;

namespace CodeSentinel.API.Services;

/// <summary>
/// Lightweight vector store backed by SQLite.
///
/// Design decisions:
///    Embeddings stored as raw little-endian float BLOBs (no Base64, no JSON).
///    A 768-dim vector = 3072 bytes. 100 000 chunks ≈ 300 MB on disk.
///    Full-table cosine scan at query time.
///    For under ~50 000 chunks this comfortably fits within the &lt;8 GB budget.
///    At larger scale: batch into pages or add an HNSW index via sqlite-vss.
///    SqliteConnection is NOT thread-safe; we use a connection-per-operation
///    pattern via SqliteConnectionStringBuilder with a shared cache connection string.
///
/// AOT safety: only BCL types and Microsoft.Data.Sqlite (which ships with
/// a native libsqlite3 and uses no reflection for its core data path).
/// </summary>
public sealed class VectorStore : IDisposable
{
    private readonly string _connectionString;

    public VectorStore(IConfiguration config)
    {
        var dbPath = config["VectorStore:Path"] ?? "codesentinel.db";
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    public async Task EnsureSchemaAsync()
    {
        await using var conn = OpenConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS chunks (
                id        TEXT    PRIMARY KEY,
                file_path TEXT    NOT NULL,
                content   TEXT    NOT NULL,
                embedding BLOB    NOT NULL,
                indexed_at INTEGER NOT NULL DEFAULT (unixepoch())
            ) WITHOUT ROWID;

            CREATE INDEX IF NOT EXISTS idx_chunks_file_path ON chunks(file_path);
            """;

        await cmd.ExecuteNonQueryAsync();

        await using var pragmaCmd = conn.CreateCommand();
        pragmaCmd.CommandText = """
            PRAGMA cache_size = -65536;
            PRAGMA mmap_size = 268435456;
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            """;
        await pragmaCmd.ExecuteNonQueryAsync();
    }

    public async Task UpsertAsync(CodeChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk.Embedding);

        await using var conn = OpenConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO chunks (id, file_path, content, embedding)
            VALUES ($id, $fp, $content, $emb)
            ON CONFLICT(id) DO UPDATE
                SET file_path  = excluded.file_path,
                    content    = excluded.content,
                    embedding  = excluded.embedding,
                    indexed_at = unixepoch();
            """;

        cmd.Parameters.AddWithValue("$id", chunk.Id);
        cmd.Parameters.AddWithValue("$fp", chunk.FilePath);
        cmd.Parameters.AddWithValue("$content", chunk.Content);
        cmd.Parameters.AddWithValue("$emb", FloatsToBytes(chunk.Embedding));

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Returns the <paramref name="topK"/> chunks most similar to <paramref name="queryEmbedding"/>.
    /// Pure in-process cosine scan — no extensions required.
    /// </summary>
    public async Task<List<CodeChunk>> SearchAsync(float[] queryEmbedding, int topK = 5)
    {
        await using var conn = OpenConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, file_path, content, embedding FROM chunks";

        var raw = new List<(string Id, string FilePath, string Content, float[] Emb)>();

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var blob = (byte[])reader["embedding"];
            raw.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                BytesToFloats(blob)));
        }

        if (raw.Count == 0) return [];

        int dim = queryEmbedding.Length;
        var corpus = new float[raw.Count * dim];
        for (int i = 0; i < raw.Count; i++)
            raw[i].Emb.CopyTo(corpus, i * dim);

        var top = VectorMath.TopK(queryEmbedding, corpus, dim, topK);

        return top
            .Select(t => new CodeChunk
            {
                Id = raw[t.Index].Id,
                FilePath = raw[t.Index].FilePath,
                Content = raw[t.Index].Content,
            })
            .ToList();
    }

    public async Task<long> CountAsync()
    {
        await using var conn = OpenConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM chunks";
        return (long)(await cmd.ExecuteScalarAsync() ?? 0L);
    }

    private SqliteConnection OpenConnection() => new(_connectionString);

    private static byte[] FloatsToBytes(float[] floats)
    {
        var bytes = new byte[floats.Length * sizeof(float)];
        MemoryMarshal.AsBytes(floats.AsSpan()).CopyTo(bytes);
        return bytes;
    }

    private static float[] BytesToFloats(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        MemoryMarshal.Cast<byte, float>(bytes).CopyTo(floats);
        return floats;
    }

    public void Dispose() { }
}
