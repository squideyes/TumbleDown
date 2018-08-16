// Copyright 2018 Louis S.Berman.
//
// This file is part of TumbleDown.
//
// TumbleDown is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published 
// by the Free Software Foundation, either version 3 of the License, 
// or (at your option) any later version.
//
// TumbleDown is distributed in the hope that it will be useful, but 
// WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the GNU 
// General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with TumbleDown.  If not, see <http://www.gnu.org/licenses/>.

using Flurl;
using Flurl.Http;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace TumbleDown
{
    public class Tumblr
    {
        public class Root
        {
            [JsonProperty(PropertyName = "posts-start")]
            public int FirstPost { get; set; }

            [JsonProperty(PropertyName = "posts-total")]
            public int TotalPosts { get; set; }

            [JsonProperty(PropertyName = "posts")]
            public Post[] Posts { get; set; }
        }

        public class Post
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }

            [JsonProperty(PropertyName = "date-gmt")]
            public DateTime DateTime { get; set; }

            [JsonProperty(PropertyName = "type")]
            private string Type { get; set; }

            [JsonProperty(PropertyName = "photo-url-1280")]
            private string PhotoUrl { get; set; }

            [JsonProperty(PropertyName = "video-player")]
            private string VideoPlayer { get; set; }

            public Media? Media
            {
                get
                {
                    switch (Type)
                    {
                        case "photo":
                            return TumbleDown.Media.Photo;
                        case "video":
                            return TumbleDown.Media.Video;
                        default:
                            return null;
                    }
                }
            }

            private HtmlNode VideoSource
            {
                get
                {
                    var doc = new HtmlDocument();

                    doc.LoadHtml(VideoPlayer);

                    var video = doc.DocumentNode
                        .Descendants("video").FirstOrDefault();

                    if (video == null)
                        return null;

                    return video.SelectNodes("source").FirstOrDefault();
                }
            }

            public Uri Uri
            {
                get
                {
                    if (!Media.HasValue)
                        return null;

                    if (Media == TumbleDown.Media.Photo)
                    {
                        return new Uri(PhotoUrl);
                    }
                    else
                    {
                        var source = VideoSource;

                        if (source == null)
                            return null;

                        return new Uri(source.Attributes["src"].Value);
                    }
                }
            }

            public string FileName
            {
                get
                {
                    var sb = new StringBuilder();

                    sb.Append(Id);

                    switch (Media)
                    {
                        case TumbleDown.Media.Video:
                            var source = VideoSource.Attributes["type"].Value.ToLower();
                            switch (source)
                            {
                                case "video/mp4":
                                    sb.Append(".mp4");
                                    break;
                                default:
                                    sb.Append(".octets");
                                    break;
                            }
                            break;
                        case TumbleDown.Media.Photo:
                            sb.Append(Path.GetExtension(Uri.AbsoluteUri).ToLower());
                            break;
                    }

                    return sb.ToString();
                }
            }

            public string GetFullPath(string folder) => Path.Combine(folder, FileName);
        }

        private readonly ILogger<Worker> logger;

        public Tumblr(ILogger<Worker> logger)
        {
            this.logger = logger;
        }

        private async Task<Root> GetRootAsync(string blogName, int start, Media media)
        {
            var url = $"http://{blogName}.tumblr.com"
                .AppendPathSegments("api", "read", "json").SetQueryParams(new { start, num = 50 });

            if (media != Media.All)
                url.SetQueryParam("type", media.ToString().ToLower());

            var data = await url.GetStringAsync();

            var json = data.Substring(data.IndexOf('{'));

            json = json.Substring(0, json.Length - 2);

            var root = JsonConvert.DeserializeObject<Root>(json);

            if (root.Posts.Count() == 0)
                return root;

            var fetched = root.Posts.Count();

            var mediaKind = media.ToString().ToUpper();

            root.Posts = root.Posts.Where(p => p.Uri != null).ToArray();

            logger.LogDebug($"Fetched {fetched} posts (Blog: {blogName}, Media: {mediaKind}, Start: {start:N0})");

            return root;
        }

        public async Task<List<Post>> GetPostsAsync(
            string blogName, string folder, Media media, bool debugMode, int threads)
        {
            var posts = new ConcurrentBag<Post>();

            Root root;

            root = await GetRootAsync(blogName, 0, media);

            if (root?.Posts?.Length > 0)
                root.Posts.Where(p => p.Uri != null).ToList().ForEach(p => posts.Add(p));

            if (debugMode || root.TotalPosts - root.FirstPost < 50)
                return posts.ToList();

            var chunks = (root.TotalPosts - root.FirstPost - 50)
                .Funcify(x => (x / 50) + (x % 50 > 0 ? 1 : 0));

            var worker = new ActionBlock<int>(
                async start =>
                {
                    root = await GetRootAsync(blogName, start, media);

                    if (root?.Posts?.Length > 0)
                        root.Posts.Where(p => p.Uri != null).ToList().ForEach(p => posts.Add(p));

                    await Task.Delay(400);
                },
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = debugMode ? 1 : threads
                });

            Enumerable.Range(1, chunks).ToList().ForEach(x => worker.Post(root.FirstPost + (x * 50)));

            worker.Complete();

            await worker.Completion;

            logger.LogDebug($"Found {posts.Count:N0} posts with valid Uris.");

            if (posts.Count == 0)
                return posts.ToList();

            var fileNames = new HashSet<string>(Directory.GetFiles(
                folder, "*.*").Select(f => Path.GetFileName(f)));

            return posts.Where(p => !fileNames.Contains(p.FileName)).ToList();
        }

        public async Task FetchAndSaveFilesAsync(string folder, List<Post> posts, bool debugMode, int threads)
        {
            var worker = new ActionBlock<Post>(
                async post =>
                {
                    HttpResponseMessage response = null;

                    try
                    {
                        response = await new HttpClient().GetAsync(post.Uri);

                        response.EnsureSuccessStatusCode();
                    }
                    catch (Exception error)
                    {
                        logger.LogWarning(error, $"Error fetching \"{post.Uri}\" (Message: {error.Message})");

                        return;
                    }

                    var fileName = Path.Combine(folder, post.FileName);

                    try
                    {
                        fileName.EnsurePathExists();

                        using (var target = File.OpenWrite(fileName))
                        {
                            var source = await response
                                .Content.ReadAsStreamAsync();

                            await source.CopyToAsync(target);
                        };

                        var fileInfo = new FileInfo(fileName)
                        {
                            CreationTimeUtc = post.DateTime,
                            LastWriteTimeUtc = post.DateTime
                        };

                        logger.LogInformation(
                            $"Downloaded {Path.GetFileName(fileName)} from {post.Uri} ({fileInfo.Length:N0} bytes)");
                    }
                    catch (Exception error)
                    {
                        logger.LogError(error, $"Error saving \"{fileName}\" (Message: {error.Message})");
                    }
                },
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = debugMode ? 1 : threads
                });

            posts.ForEach(post => worker.Post(post));

            worker.Complete();

            await worker.Completion;
        }
    }
}
