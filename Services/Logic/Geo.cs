using AudioGuide.Api.Common;

namespace AudioGuide.Api.Services.Logic
{
    /*
     * Hàm tính toán địa lý THUẦN (không đọc database, không gọi mạng) -> dễ viết unit test.
     */
    public static class Geo
    {
        public const double EarthRadiusM = 6_371_000;

        /*
         * Khoảng cách đường chim bay giữa 2 tọa độ GPS (mét) - công thức Haversine.
         * Trái Đất hình cầu nên không dùng Pythagoras trên vĩ độ/kinh độ được:
         *   a = sin²(Δvĩ/2) + cos(vĩ1)·cos(vĩ2)·sin²(Δkinh/2);  d = 2·R·asin(√a)
         */
        public static double HaversineM(double lat1, double lng1, double lat2, double lng2)
        {
            double Rad(double deg) => deg * Math.PI / 180;
            var p1 = Rad(lat1);
            var p2 = Rad(lat2);
            var dPhi = Rad(lat2 - lat1);
            var dLambda = Rad(lng2 - lng1);
            var a = Math.Pow(Math.Sin(dPhi / 2), 2) + Math.Cos(p1) * Math.Cos(p2) * Math.Pow(Math.Sin(dLambda / 2), 2);
            return 2 * EarthRadiusM * Math.Asin(Math.Sqrt(a));
        }

        // Thời gian đi bộ (phút) với tốc độ tham quan chậm ~60 m/phút
        public static double WalkingMinutes(double distanceM)
            => Math.Round(distanceM / BusinessConstants.WalkingSpeedMPerMin, 1);

        // Thời gian nghe thuyết minh ước tính theo số từ
        public static double EstimateListenMinutes(string text, int wordsPerMinute = BusinessConstants.ListenWordsPerMinute)
        {
            var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            return Math.Round(Math.Max(words, 1) / (double)wordsPerMinute, 1);
        }

        /*
         * Sắp xếp lộ trình theo thuật toán "láng giềng gần nhất": từ vị trí hiện tại luôn đi tới điểm CHƯA thăm gần nhất.
         * Không tối ưu tuyệt đối (bài toán người du lịch - TSP) nhưng đủ tốt với < 20 điểm và rất nhanh.
         */
        public static List<T> NearestNeighborOrder<T>((double Lat, double Lng) start, IEnumerable<T> items, Func<T, (double Lat, double Lng)> coords)
        {
            var remaining = items.ToList();
            var route = new List<T>();
            var current = start;
            while (remaining.Count > 0)
            {
                var next = remaining.MinBy(it =>
                {
                    var c = coords(it);
                    return HaversineM(current.Lat, current.Lng, c.Lat, c.Lng);
                })!;
                route.Add(next);
                remaining.Remove(next);
                current = coords(next);
            }
            return route;
        }

        // Tổng quãng đường (mét) đi lần lượt qua các điểm theo đúng thứ tự cho trước
        public static double TotalDistanceM(IReadOnlyList<(double Lat, double Lng)> points)
        {
            double total = 0;
            for (var i = 1; i < points.Count; i++)
                total += HaversineM(points[i - 1].Lat, points[i - 1].Lng, points[i].Lat, points[i].Lng);
            return Math.Round(total, 1);
        }
    }
}
