namespace CampusMarket.ML.Training.Evaluation;

// Evaluation of the similar-listings model: for every listing, the k most similar other listings
// (cosine similarity of their text vectors) and the share of them that have the same category,
// averaged over all listings (precision@k).
//
// Listings with the same TemplateId are variations of the same item (the same book, the same
// laptop), so finding them is trivial; they are left out of each other's candidates, which makes
// the score measure whether the model finds DIFFERENT items of the same kind.
public static class SimilarityEvaluation
{
    public const int DefaultK = 5;

    // A text vector as its non-zero entries, indices in increasing order (how ML.NET stores a
    // sparse vector), so the dot product of two vectors only walks their non-zero entries.
    public sealed record SparseVector(int[] Indices, float[] Values)
    {
        public static SparseVector FromDense(IReadOnlyList<float> dense)
        {
            var indices = new List<int>();
            var values = new List<float>();
            for (var i = 0; i < dense.Count; i++)
            {
                if (dense[i] == 0)
                    continue;
                indices.Add(i);
                values.Add(dense[i]);
            }

            return new SparseVector([.. indices], [.. values]);
        }
    }

    public sealed record Item(SparseVector Vector, string Category, string Group);

    public static double Dot(SparseVector a, SparseVector b)
    {
        double sum = 0;
        int i = 0, j = 0;
        while (i < a.Indices.Length && j < b.Indices.Length)
        {
            if (a.Indices[i] == b.Indices[j])
                sum += (double)a.Values[i++] * b.Values[j++];
            else if (a.Indices[i] < b.Indices[j])
                i++;
            else
                j++;
        }

        return sum;
    }

    public static double Norm(SparseVector v) => Math.Sqrt(Dot(v, v));

    // Cosine similarity; 0 when one of the vectors is empty (a listing with no known word).
    public static double Cosine(SparseVector a, SparseVector b)
    {
        var norms = Norm(a) * Norm(b);
        return norms == 0 ? 0 : Dot(a, b) / norms;
    }

    // precision@k of the model: the share of the k most similar candidates with the same category.
    // Items with no candidate at all (every other item is in their group) are skipped.
    public static double PrecisionAtK(IReadOnlyList<Item> items, int k = DefaultK)
    {
        var norms = items.Select(x => Norm(x.Vector)).ToArray();
        var scores = new List<double>(items.Count);

        for (var q = 0; q < items.Count; q++)
        {
            var top = Enumerable.Range(0, items.Count)
                .Where(c => c != q && items[c].Group != items[q].Group)
                .Select(c => (Index: c, Similarity: norms[q] * norms[c] == 0 ? 0 : Dot(items[q].Vector, items[c].Vector) / (norms[q] * norms[c])))
                .OrderByDescending(x => x.Similarity)
                .ThenBy(x => x.Index)
                .Take(k)
                .ToList();

            if (top.Count == 0)
                continue;

            scores.Add(top.Count(x => items[x.Index].Category == items[q].Category) / (double)top.Count);
        }

        return scores.Count == 0 ? 0 : scores.Average();
    }

    // What k listings picked at random among the same candidates would score: for every item, the
    // share of its candidates that have its category (the expected precision of a random pick).
    public static double RandomPrecision(IReadOnlyList<Item> items)
    {
        var scores = new List<double>(items.Count);
        for (var q = 0; q < items.Count; q++)
        {
            var candidates = items.Where((c, i) => i != q && c.Group != items[q].Group).ToList();
            if (candidates.Count == 0)
                continue;
            scores.Add(candidates.Count(c => c.Category == items[q].Category) / (double)candidates.Count);
        }

        return scores.Count == 0 ? 0 : scores.Average();
    }
}
