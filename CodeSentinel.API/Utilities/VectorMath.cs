using System.Numerics;

namespace CodeSentinel.API.Utilities;

/// <summary>
/// Hardware-accelerated cosine similarity using System.Numerics.Vector<float>.
/// On x64 with AVX2 this processes 8 floats per cycle.
/// On ARM with NEON it processes 4 floats per cycle.
/// Falls back to scalar on unsupported hardware — no runtime check needed;
/// the JIT handles it transparently (and under Native AOT, the LLVM backend does too).
/// </summary>
public static class VectorMath
{
    /// <summary>
    /// Cosine similarity ∈ [-1, 1].
    /// Returns 0 when either vector is zero-magnitude.
    /// </summary>
    public static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
            ThrowDimensionMismatch(a.Length, b.Length);

        int width = Vector<float>.Count;
        int i = 0;
        float dot = 0f, magA = 0f, magB = 0f;

        for (; i <= a.Length - width; i += width)
        {
            var va = new Vector<float>(a.Slice(i));
            var vb = new Vector<float>(b.Slice(i));
            dot += Vector.Dot(va, vb);
            magA += Vector.Dot(va, va);
            magB += Vector.Dot(vb, vb);
        }

        for (; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        float denom = MathF.Sqrt(magA) * MathF.Sqrt(magB);
        return denom == 0f ? 0f : dot / denom;
    }

    /// <summary>
    /// Finds the top-K most similar vectors to <paramref name="query"/>.
    /// Uses a min-heap approach: O(n·d + n·log K) vs O(n·d·log n) for full sort.
    /// </summary>
    public static List<(int Index, float Score)> TopK(
        ReadOnlySpan<float> query,
        ReadOnlySpan<float> corpus,
        int dimensions,
        int k)
    {
        int count = corpus.Length / dimensions;
        var heap = new PriorityQueue<(int Index, float Score), float>();

        for (int i = 0; i < count; i++)
        {
            var candidate = corpus.Slice(i * dimensions, dimensions);
            float score = CosineSimilarity(query, candidate);

            if (heap.Count < k)
            {
                heap.Enqueue((i, score), -score);
            }
            else
            {
                var (minIdx, minScore) = heap.Peek();
                if (score > minScore)
                {
                    heap.Dequeue();
                    heap.Enqueue((i, score), -score);
                }
            }
        }

        var results = new List<(int Index, float Score)>(heap.Count);
        while (heap.Count > 0)
        {
            var (idx, score) = heap.Dequeue();
            results.Add((idx, score));
        }

        results.Sort(static (x, y) => y.Score.CompareTo(x.Score));
        return results;
    }

    private static void ThrowDimensionMismatch(int a, int b) =>
        throw new ArgumentException($"Vector dimension mismatch: {a} vs {b}.");
}
