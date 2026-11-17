using AudioGuide.Api.Services.Logic;

namespace AudioGuide.Tests.Logic
{
    public class Bm25AndTextTests
    {
        [Fact]
        public void Normalize_RemovesVietnameseDiacritics()
            => Assert.Equal("quan the am bo tat dai", TextTools.Normalize("Quan Thế Âm Bồ Tát Đài"));

        [Fact]
        public void Tokenize_DropsStopWordsAndShortTokens()
        {
            var tokens = Bm25Index.Tokenize("Tượng Quan Âm cao bao nhiêu mét?");
            Assert.Contains("tuong", tokens);
            Assert.Contains("quan", tokens);
            Assert.DoesNotContain("bao", tokens);
            Assert.DoesNotContain("nhieu", tokens);
        }

        [Fact]
        public void Search_FindsMostRelevantDocument_EvenWithoutAccents()
        {
            var index = new Bm25Index(new[]
            {
                new SearchDocument("poi:1", "poi", "Cổng Tam Quan", "Cổng chính của chùa có ba lối đi."),
                new SearchDocument("poi:2", "poi", "Tượng Phật Bà Quan Thế Âm", "Tượng cao 67 mét hướng ra biển."),
                new SearchDocument("article:1", "article", "Giờ mở cửa", "Chùa mở cửa từ 6 giờ sáng.")
            });
            var hits = index.Search("tuong quan the am cao bao nhieu");
            Assert.Equal("poi:2", hits[0].Document.Id);
        }

        [Fact]
        public void Search_UnrelatedQuestion_ReturnsNothing()
        {
            var index = new Bm25Index(new[] { new SearchDocument("1", "poi", "Cổng Tam Quan", "Ba lối đi") });
            Assert.Empty(index.Search("lập trình python"));
        }

        [Fact]
        public void BestSentences_PicksSentencesMatchingQuestion_InOriginalOrder()
        {
            var text = "Chùa nằm ở Sơn Trà. Tượng cao 67 mét. Có bãi gửi xe rộng.";
            Assert.Equal("Tượng cao 67 mét.", Bm25Index.BestSentences(text, "tượng cao bao nhiêu", 1));
        }

        [Fact]
        public void SourceHash_ChangesWhenContentChanges()
        {
            Assert.Equal(TextTools.SourceHash("A", "B"), TextTools.SourceHash("A", "B"));
            Assert.NotEqual(TextTools.SourceHash("A", "B"), TextTools.SourceHash("A", "C"));
        }
    }
}
