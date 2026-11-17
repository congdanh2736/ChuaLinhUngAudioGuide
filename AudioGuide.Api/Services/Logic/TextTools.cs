using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AudioGuide.Api.Services.Logic
{
    public static class TextTools
    {
        // MD5 dạng chữ thường - chỉ để so sánh nội dung, không dùng cho bảo mật
        public static string Md5(string text)
            => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

        // Hash nội dung gốc (tên + mô tả) để biết bản dịch còn khớp nội dung hiện tại hay không
        public static string SourceHash(string name, string description) => Md5($"{name}\n{description}");

        public static string TipsHash(string tips) => Md5(tips);

        // Chữ thường + bỏ dấu tiếng Việt: "Quan Thế Âm" -> "quan the am" (khớp cả khi khách gõ không dấu)
        public static string Normalize(string text)
        {
            var lower = text.ToLowerInvariant().Replace('đ', 'd');
            var decomposed = lower.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
