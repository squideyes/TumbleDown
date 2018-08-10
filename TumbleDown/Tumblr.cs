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
using System.Linq;
using Flurl.Http;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Net.Http;
using System.IO;
using System;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace TumbleDown
{
    public class Tumblr
    {
        public class Root
        {
            [JsonProperty(PropertyName = "tumblelog")]
            public Info Info { get; set; }

            [JsonProperty(PropertyName = "posts-start")]
            public int FirstPost { get; set; }

            [JsonProperty(PropertyName = "posts-total")]
            public int TotalPosts { get; set; }

            [JsonProperty(PropertyName = "posts")]
            public Post[] Posts { get; set; }
        }

        public class Info
        {
            [JsonProperty(PropertyName = "title")]
            public string Title { get; set; }

            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "description")]
            public string Description { get; set; }
        }

        public class Post
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }

            [JsonProperty(PropertyName = "type")]
            public string Type { get; set; }

            [JsonProperty(PropertyName = "date-gmt")]
            public DateTime DateTime { get; set; }

            [JsonProperty(PropertyName = "photo-url-1280")]
            public string Url { get; set; }

            public string FileName
            {
                get
                {
                    var sb = new StringBuilder();

                    sb.Append(Id);
                    sb.Append(Path.GetExtension(Url).ToLower());

                    return sb.ToString();
                }
            }

            public string GetFullPath(string basePath) =>
                Path.Combine(basePath, FileName);
        }

        private HttpClient client = new HttpClient();

        private readonly ILogger<Worker> logger;

        public Tumblr(ILogger<Worker> logger)
        {
            this.logger = logger;
        }

        private async Task<Root> GetRoot(string blogName, int start, Media media)
        {
            var url = $"http://{blogName}.tumblr.com"
                .AppendPathSegments("api", "read", "json")
                .SetQueryParams(new { start, num = 50 });

            if (media != Media.All)
                url.SetQueryParam("type", media.ToString().ToLower());

            var data = await url.GetStringAsync();

            var json = data.Substring(data.IndexOf('{'));

            json = json.Substring(0, json.Length - 2);

            return JsonConvert.DeserializeObject<Root>(json);
        }

        public async Task<List<Post>> GetPostsAsync(
            string blogName, string basePath, Media media)
        {
            var posts = new List<Post>();

            int start = 0;

            Root root;

            do
            {
                root = await GetRoot(blogName, start, media);

                if (root?.Posts?.Length > 0)
                    posts.AddRange(root.Posts);

                start += 50;
            }
            while (root?.Posts?.Count() > 0);

            if (posts.Count == 0)
                return posts;

            var fileNames = new HashSet<string>(Directory.GetFiles(
                basePath, "*.*").Select(f => Path.GetFileName(f)));

            return posts.Where(p => !string.IsNullOrWhiteSpace(p.Url)
                && !fileNames.Contains(p.FileName)).ToList();
        }

        public async Task FetchAndSaveFilesAsync(string basePath, List<Post> posts)
        {
            var worker = new ActionBlock<Post>(
                async post =>
                {
                    if (!Uri.TryCreate(post.Url, UriKind.Absolute, out Uri uri))
                        return;

                    var response = await client.GetAsync(post.Url);

                    response.EnsureSuccessStatusCode();

                    var fileName = Path.Combine(basePath, post.FileName);

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

                    logger.LogInformation($"Downloaded \"{fileName}\"");
                });

            posts.ForEach(post => worker.Post(post));

            worker.Complete();

            await worker.Completion;
        }
    }
}
