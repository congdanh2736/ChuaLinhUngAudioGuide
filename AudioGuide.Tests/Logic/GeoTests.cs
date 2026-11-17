using AudioGuide.Api.Services.Logic;

namespace AudioGuide.Tests.Logic
{
    // Test các hàm tính toán địa lý (hàm thuần -> không cần database)
    public class GeoTests
    {
        [Fact]
        public void Haversine_SamePoint_IsZero()
            => Assert.Equal(0, Geo.HaversineM(16.1, 108.27, 16.1, 108.27), 6);

        [Fact]
        public void Haversine_OneThousandthDegreeLatitude_IsAbout111Meters()
            => Assert.InRange(Geo.HaversineM(16.1000, 108.2780, 16.1010, 108.2780), 110.5, 111.6);

        [Fact]
        public void Haversine_IsSymmetric()
        {
            var a = Geo.HaversineM(16.1001, 108.2773, 16.1035, 108.2779);
            var b = Geo.HaversineM(16.1035, 108.2779, 16.1001, 108.2773);
            Assert.Equal(a, b, 6);
        }

        [Fact]
        public void WalkingMinutes_Uses60MetersPerMinute()
            => Assert.Equal(5, Geo.WalkingMinutes(300));

        [Fact]
        public void EstimateListenMinutes_150WordsPerMinute()
            => Assert.Equal(2, Geo.EstimateListenMinutes(string.Join(" ", Enumerable.Repeat("chua", 300))));

        [Fact]
        public void NearestNeighbor_AlwaysGoesToClosestUnvisited()
        {
            var points = new[] { ("C", 16.1020, 108.2790), ("A", 16.1003, 108.2779), ("B", 16.1010, 108.2785) };
            var order = Geo.NearestNeighborOrder((16.1000, 108.2775), points, p => (p.Item2, p.Item3));
            Assert.Equal(new[] { "A", "B", "C" }, order.Select(p => p.Item1));
        }

        [Fact]
        public void TotalDistance_IsSumOfLegs()
        {
            var a = (16.1003, 108.2779);
            var b = (16.1010, 108.2785);
            var c = (16.1020, 108.2790);
            var expected = Math.Round(Geo.HaversineM(a.Item1, a.Item2, b.Item1, b.Item2) + Geo.HaversineM(b.Item1, b.Item2, c.Item1, c.Item2), 1);
            Assert.Equal(expected, Geo.TotalDistanceM(new[] { a, b, c }));
        }

        [Fact]
        public void TotalDistance_LessThanTwoPoints_IsZero()
        {
            Assert.Equal(0, Geo.TotalDistanceM(Array.Empty<(double, double)>()));
            Assert.Equal(0, Geo.TotalDistanceM(new[] { (16.1, 108.2) }));
        }
    }
}
