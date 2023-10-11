﻿using Lampac.Models.SISI;
using Shared.Model.SISI;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.SISI
{
    public static class XnxxTo
    {
        public static ValueTask<string?> InvokeHtml(string host, string? search, int pg, Func<string, ValueTask<string?>> onresult)
        {
            string url = $"{host}/best/{DateTime.Today.AddMonths(-1):yyyy-MM}/{pg}";
            if (!string.IsNullOrWhiteSpace(search))
                url = $"{host}/search/{HttpUtility.UrlEncode(search)}/{pg}";

            return onresult.Invoke(url);
        }

        public static List<PlaylistItem> Playlist(string uri, string html, Func<PlaylistItem, PlaylistItem>? onplaylist = null)
        {
            var playlists = new List<PlaylistItem>() { Capacity = 40 };

            foreach (string row in html.Split("<div id=\"video_").Skip(1))
            {
                var g = Regex.Match(row, "<a href=\"/(video-[^\"]+)\" title=\"([^\"]+)\"").Groups;
                string quality = Regex.Match(row, "<span class=\"superfluous\"> - </span>([^<]+)</span>").Groups[1].Value;

                if (!string.IsNullOrEmpty(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                {
                    string duration = Regex.Match(row, "</span>([^<]+)<span class=\"video-hd\">").Groups[1].Value.Trim();
                    string img = Regex.Match(row, "data-src=\"([^\"]+)\"").Groups[1].Value.Replace(".THUMBNUM.", ".1.");

                    var pl = new PlaylistItem()
                    {
                        name = g[2].Value,
                        video = $"{uri}?uri={g[1].Value}",
                        picture = img,
                        time = duration,
                        quality = string.IsNullOrWhiteSpace(quality) ? null : quality,
                        json = true,
                        bookmark = new Bookmark()
                        {
                            site = "xnx",
                            href = g[1].Value,
                            image = img
                        }
                    };

                    if (onplaylist != null)
                        pl = onplaylist.Invoke(pl);

                    playlists.Add(pl);
                }
            }

            return playlists;
        }

        public static List<MenuItem> Menu(string? host)
        {
            host = string.IsNullOrWhiteSpace(host) ? string.Empty : $"{host}/";

            return new List<MenuItem>()
            {
                new MenuItem()
                {
                    title = "Поиск",
                    search_on = "search_on",
                    playlist_url = host + "xnx",
                }
            };
        }

        async public static ValueTask<Dictionary<string, string>?> StreamLinks(string host, string? uri, Func<string, ValueTask<string?>> onresult, Func<string, ValueTask<string?>>? onm3u = null)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return null;

            string? html = await onresult.Invoke($"{host}/{Regex.Replace(uri ?? string.Empty, "^([^/]+)/.*", "$1/_")}");
            if (html == null)
                return null;

            string stream_link = new Regex("html5player\\.setVideoHLS\\('([^']+)'\\);").Match(html).Groups[1].Value;
            if (string.IsNullOrWhiteSpace(stream_link))
                return null;

            string? m3u8 = onm3u == null ? null : await onm3u.Invoke(stream_link);
            if (m3u8 == null)
            {
                return new Dictionary<string, string>()
                {
                    ["auto"] = stream_link
                };
            }

            var stream_links = new Dictionary<int, string>();
            foreach (Match m in Regex.Matches(m3u8, "(hls-(2160|1440|1080|720|480|360)p[^\n\r\t ]+)"))
            {
                string hls = m.Groups[1].Value;
                if (string.IsNullOrEmpty(hls))
                    continue;

                hls = $"{Regex.Replace(stream_link, "/hls\\.m3u.*", "")}/{hls}".Replace("https:", "http:");
                stream_links.Add(int.Parse(m.Groups[2].Value), hls);
            }

            return stream_links.OrderByDescending(i => i.Key).ToDictionary(k => $"{k.Key}p", v => v.Value);
        }
    }
}
