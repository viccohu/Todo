using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Memo.Services
{
    /// <summary>
    /// 抓取网页标题：成功返回 &lt;title&gt;，失败/超时回退为域名。结果按 URL 缓存。
    /// </summary>
    public static class UrlTitleResolver
    {
        private static readonly ConcurrentDictionary<string, string> Cache = new();

        private static readonly HttpClient Http = CreateClient();

        private static readonly Regex TitleRegex = new(
            @"<title[^>]*>\s*(.+?)\s*</title>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static HttpClient CreateClient()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
            };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
            return client;
        }

        /// <summary>判断文本是否是一个裸 http/https 链接（无空白、无其他内容）。</summary>
        public static bool IsBareUrl(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            text = text.Trim();
            if (!text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return false;
            if (text.Contains(' ') || text.Contains('\n') || text.Contains('\r') || text.Contains('\t'))
                return false;
            return Uri.TryCreate(text, UriKind.Absolute, out _);
        }

        /// <summary>URL 的域名部分，解析失败时返回原文。用作抓取标题前的占位或失败回退。</summary>
        public static string GetDomainFallback(string url)
        {
            return Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
                ? uri.Host
                : (url ?? string.Empty);
        }

        /// <summary>异步抓取网页标题，失败回退为域名。永不抛异常。</summary>
        public static async Task<string> ResolveAsync(string url, CancellationToken cancellation = default)
        {
            url = (url ?? string.Empty).Trim();
            var fallback = GetDomainFallback(url);
            if (!IsBareUrl(url))
                return fallback;

            if (Cache.TryGetValue(url, out var cached))
                return cached;

            try
            {
                using var response = await Http.GetAsync(
                    url, HttpCompletionOption.ResponseHeadersRead, cancellation);
                if (!response.IsSuccessStatusCode)
                    return fallback;

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (mediaType != null && !mediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
                    return fallback;

                // 只读取前 64KB，避免大页面拖慢
                using var stream = await response.Content.ReadAsStreamAsync(cancellation);
                var buffer = new byte[64 * 1024];
                var total = 0;
                while (total < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellation);
                    if (read <= 0)
                        break;
                    total += read;
                }

                var html = System.Text.Encoding.UTF8.GetString(buffer, 0, total);
                var match = TitleRegex.Match(html);
                if (!match.Success)
                    return fallback;

                var title = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value)
                    .Replace('\n', ' ').Replace('\r', ' ').Trim();
                if (string.IsNullOrWhiteSpace(title))
                    return fallback;

                // 标题中的方括号会破坏 [标题](url) 存储格式
                title = title.Replace('[', '(').Replace(']', ')');
                if (title.Length > 80)
                    title = title[..80] + "…";

                Cache[url] = title;
                return title;
            }
            catch
            {
                return fallback;
            }
        }
    }
}
