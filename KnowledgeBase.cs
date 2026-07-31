using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SmartGooseAI
{
    public class KnowledgeChunk
    {
        public string Text { get; set; }
        public string SourceFile { get; set; }
        public string ChunkId { get; set; }
        public double QualityScore { get; set; } // Новый параметр качества
        public double FinalScore { get; set; }
    }

    public class SearchResult
    {
        public string Text { get; set; }
        public string SourceFile { get; set; }
        public double RelevanceScore { get; set; }
    }

    public class RAGResponse
    {
        public string Context { get; set; }
        public List<string> Sources { get; set; }
        public double Confidence { get; set; }
        public bool IsGrounded { get; set; }
    }

    public static class KnowledgeBase
    {
        private static List<KnowledgeChunk> _chunks = new List<KnowledgeChunk>();
        private static readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private static bool _isLoaded = false;

        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "и", "в", "во", "не", "что", "он", "на", "я", "с", "со", "как", "а", "то", "все", "она", "так",
            "его", "но", "да", "ты", "к", "у", "же", "вы", "за", "бы", "по", "только", "ее", "мне", "было",
            "вот", "от", "меня", "еще", "нет", "о", "из", "ему", "теперь", "когда", "даже", "ну", "вдруг",
            "ли", "если", "уже", "или", "ни", "быть", "был", "него", "до", "вас", "нибудь", "опять", "уж",
            "вам", "ведь", "там", "потом", "себя", "ничего", "ей", "может", "они", "тут", "где", "есть",
            "надо", "ней", "для", "мы", "тебя", "их", "чем", "была", "сам", "чтоб", "без", "будто", "чего",
            "the", "and", "to", "of", "a", "in", "is", "it", "you", "that", "he", "was", "for", "on", "are"
        };

        public static void Load(string folderPath, bool forceReload = false)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_isLoaded && !forceReload) return;
                _chunks.Clear();
                _isLoaded = false;

                if (!Directory.Exists(folderPath)) return;

                var files = Directory.GetFiles(folderPath, "*.json*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".json") || f.EndsWith(".jsonl")).ToArray();

                foreach (var file in files)
                {
                    try
                    {
                        var fileName = Path.GetFileName(file);
                        if (file.EndsWith(".jsonl"))
                        {
                            foreach (var line in File.ReadLines(file, Encoding.UTF8))
                            {
                                if (string.IsNullOrWhiteSpace(line)) continue;
                                try
                                {
                                    var token = JToken.Parse(line);
                                    ExtractAndScore(token, fileName);
                                }
                                catch { }
                            }
                        }
                        else
                        {
                            ExtractAndScore(JToken.Parse(File.ReadAllText(file, Encoding.UTF8)), fileName);
                        }
                    }
                    catch { }
                }

                _isLoaded = true;
            }
            finally { _lock.ExitWriteLock(); }
        }

        private static void ExtractAndScore(JToken token, string sourceFile, int depth = 0)
        {
            if (depth > 20) return;

            if (token.Type == JTokenType.String)
            {
                string text = token.ToString().Trim();

                // 🔥 УСИЛЕННЫЙ ФИЛЬТР КОДА
                if (IsCodeOrLowQuality(text)) return;

                if (text.Length > 10 && text.Length <= 500)
                {
                    double quality = CalculateTextQuality(text);
                    if (quality > 0.3) // Только качественные тексты
                    {
                        _chunks.Add(new KnowledgeChunk
                        {
                            Text = CleanText(text),
                            SourceFile = sourceFile,
                            QualityScore = quality
                        });
                    }
                }
            }
            else if (token is JContainer container)
            {
                foreach (var child in container.Children())
                    ExtractAndScore(child, sourceFile, depth + 1);
            }
        }

        // 🔥 УСИЛЕННАЯ ПРОВЕРКА НА КОД И МУСОР
        private static bool IsCodeOrLowQuality(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 10) return true;

            // 1. Признаки кода (Lua, C#, Python и т.д.)
            var codePatterns = new[]
            {
                @"function\s+\w+",      // function name
                @"\w+\.\w+\(",          // object.method(
                @"local\s+\w+\s*=",     // local var =
                @"end\s*$",             // end в конце строки
                @"if\s+.+\sthen",       // if ... then
                @"return\s+",           // return
                @"for\s+.+\sdo",        // for ... do
                @"while\s+.+\sdo",      // while ... do
                @"\w+\s*=\s*function",  // var = function
                @":\w+\(",              // :method(
                @"game\.",              // game.Players
                @"script\.",            // script.Parent
                @"workspace\.",         // workspace.Part
            };

            foreach (var pattern in codePatterns)
            {
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                    return true;
            }

            // 2. Слишком много спецсимволов
            int specialChars = text.Count(c => c == '{' || c == '}' || c == '(' || c == ')' ||
                                                c == '=' || c == ';' || c == ':' || c == '/' || c == '\\');
            if (specialChars > text.Length / 5) return true;

            // 3. Это вопрос (а нам нужны ответы!)
            if (text.Trim().EndsWith("?") && text.Length < 50) return true;

            // 4. Слишком коротко или бессмысленно
            if (text.Length < 15 && !text.Contains(" ")) return true;

            return false;
        }

        // 🔥 ОЦЕНКА КАЧЕСТВА ТЕКСТА (насколько это хороший ответ)
        private static double CalculateTextQuality(string text)
        {
            double score = 1.0;

            // + Бонус за длину (оптимально 50-300 символов)
            if (text.Length >= 50 && text.Length <= 300) score += 0.3;
            else if (text.Length < 30) score -= 0.3;

            // + Бонус за наличие точек (полные предложения)
            if (text.Contains(". ")) score += 0.2;

            // + Бонус за информативность (много букв, мало цифр)
            double letterRatio = (double)text.Count(char.IsLetter) / text.Length;
            if (letterRatio > 0.7) score += 0.2;

            // - Штраф за вопросы
            if (text.Contains("?")) score -= 0.5;

            // - Штраф за код
            if (Regex.IsMatch(text, @"\b(function|end|local|return|if|then|else|var|const|let)\b"))
                score -= 0.8;

            return Math.Max(0, score);
        }

        private static string CleanText(string text)
        {
            text = Regex.Replace(text, @"\s+", " ");
            return text.Trim();
        }

        public static SearchResult[] Search(string query, int topK = 3)
        {
            _lock.EnterReadLock();
            try
            {
                if (!_isLoaded || string.IsNullOrWhiteSpace(query))
                    return Array.Empty<SearchResult>();

                var queryWords = query.ToLower()
                    .Split(new[] { ' ', ',', '.', '!', '?', '-', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 2 && !StopWords.Contains(w))
                    .Distinct()
                    .ToList();

                if (queryWords.Count == 0) return Array.Empty<SearchResult>();

                //  УМНЫЙ ПОИСК С РАНЖИРОВАНИЕМ
                var results = _chunks
                    .Select(chunk =>
                    {
                        string lowerText = chunk.Text.ToLower();
                        int matches = queryWords.Count(w => lowerText.Contains(w));
                        double wordMatchScore = (double)matches / queryWords.Count;

                        // 🔥 Приоритет: качество текста + совпадение слов
                        double finalScore = (wordMatchScore * 0.6) + (chunk.QualityScore * 0.4);

                        return new
                        {
                            chunk.Text,
                            chunk.SourceFile,
                            Score = finalScore
                        };
                    })
                    .Where(r => r.Score > 0.2) // Отсекаем совсем нерелевантное
                    .OrderByDescending(r => r.Score)
                    .Take(topK)
                    .Select(r => new SearchResult
                    {
                        Text = r.Text,
                        SourceFile = r.SourceFile,
                        RelevanceScore = r.Score
                    })
                    .ToArray();

                return results;
            }
            finally { _lock.ExitReadLock(); }
        }

        public static RAGResponse ProcessQuery(string query, int topK = 3)
        {
            var results = Search(query, topK);
            if (results.Length == 0)
                return new RAGResponse { IsGrounded = false, Confidence = 0 };

            var bestResult = results[0];

            return new RAGResponse
            {
                Context = bestResult.Text,
                Confidence = bestResult.RelevanceScore,
                IsGrounded = bestResult.RelevanceScore > 0.4,
                Sources = new List<string> { bestResult.SourceFile }
            };
        }

        public static int GetChunkCount()
        {
            _lock.EnterReadLock();
            try { return _chunks.Count; }
            finally { _lock.ExitReadLock(); }
        }
    }
}
