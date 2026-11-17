using System.Text.RegularExpressions;

namespace AudioGuide.Api.Services.Logic
{
    public record SearchDocument(string Id, string Type, string Title, string Text);
    public record SearchHit(SearchDocument Document, double Score);

    /*
     * Tìm đoạn văn liên quan (phần "R" - Retrieval trong RAG) bằng thuật toán BM25.
     * BM25 là thuật toán xếp hạng từ khóa kinh điển (Elasticsearch dùng mặc định), miễn phí,
     * đủ tốt với kho tài liệu nhỏ của 1 ngôi chùa.
     *   điểm(tài liệu) = Σ IDF(từ) · tf·(k1+1) / (tf + k1·(1 - b + b·độ dài / độ dài TB))
     */
    public class Bm25Index
    {
        private static readonly HashSet<string> StopWords = new()
        {
            // tiếng Việt (đã bỏ dấu)
            "la", "va", "cua", "co", "cac", "nhung", "mot", "nay", "do", "o", "tai", "cho", "voi", "duoc", "khong", "gi",
            "nao", "the", "thi", "ma", "nhu", "toi", "ban", "minh", "em", "anh", "chi", "a", "oi", "nhe", "vay", "sao",
            "bao", "nhieu", "khi", "trong", "tren", "den", "tu", "ve", "hay",
            // tiếng Anh
            "is", "are", "an", "of", "to", "in", "and", "what", "where", "how", "when", "can", "i", "you", "it", "this",
            "that", "for", "on", "at", "be", "does"
        };

        private readonly List<SearchDocument> _documents;
        private readonly List<List<string>> _tokens;
        private readonly Dictionary<string, double> _idf = new();
        private readonly double _avgLen;
        private const double K1 = 1.5;
        private const double B = 0.75;

        public Bm25Index(IEnumerable<SearchDocument> documents)
        {
            _documents = documents.ToList();
            // Tiêu đề được lặp 2 lần để tăng trọng số
            _tokens = _documents.Select(d => Tokenize($"{d.Title} {d.Title} {d.Text}")).ToList();
            _avgLen = _tokens.Count > 0 ? _tokens.Average(t => t.Count) : 0;
            var df = new Dictionary<string, int>();
            foreach (var term in _tokens.SelectMany(t => t.Distinct()))
                df[term] = df.GetValueOrDefault(term) + 1;
            var n = _documents.Count;
            foreach (var (term, freq) in df)
                _idf[term] = Math.Log(1 + (n - freq + 0.5) / (freq + 0.5));
        }

        public IReadOnlyList<SearchDocument> Documents => _documents;

        public static List<string> Tokenize(string text)
            => Regex.Matches(TextTools.Normalize(text), @"\w+")
                .Select(m => m.Value)
                .Where(t => t.Length > 1 && !StopWords.Contains(t))
                .ToList();

        public List<SearchHit> Search(string query, int topK = 3)
        {
            var terms = Tokenize(query);
            var hits = new List<SearchHit>();
            for (var i = 0; i < _documents.Count; i++)
            {
                var toks = _tokens[i];
                if (toks.Count == 0) continue;
                var tf = toks.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
                double score = 0;
                foreach (var term in terms)
                {
                    if (!tf.TryGetValue(term, out var freq)) continue;
                    var denom = freq + K1 * (1 - B + B * toks.Count / (_avgLen == 0 ? 1 : _avgLen));
                    score += _idf.GetValueOrDefault(term) * freq * (K1 + 1) / denom;
                }
                if (score > 0) hits.Add(new SearchHit(_documents[i], score));
            }
            return hits.OrderByDescending(h => h.Score).Take(topK).ToList();
        }

        public static List<string> SplitSentences(string text)
            => Regex.Split(text, @"(?<=[.!?])\s+|\n+").Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        // Trích các câu liên quan nhất tới câu hỏi (trả lời kiểu trích đoạn khi không có AI)
        public static string BestSentences(string text, string query, int limit = 2)
        {
            var sentences = SplitSentences(text);
            if (sentences.Count == 0) return text;
            var q = Tokenize(query).ToHashSet();
            var top = sentences
                .Select((s, i) => (Score: Tokenize(s).ToHashSet().Intersect(q).Count(), Index: i, Text: s))
                .OrderByDescending(x => x.Score).ThenBy(x => x.Index)
                .Take(limit)
                .OrderBy(x => x.Index);
            return string.Join(" ", top.Select(x => x.Text));
        }
    }
}
