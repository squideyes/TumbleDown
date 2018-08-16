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

using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using System;
using System.Text.RegularExpressions;

namespace TumbleDown
{
    public class Worker
    {
        private static readonly AppInfo appInfo =
            new AppInfo(typeof(Worker).Assembly);

        private readonly ILogger<Worker> logger;

        public Worker(ILogger<Worker> logger)
        {
            this.logger = logger;
        }

        public int Run(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "TumbleDown",
                Description = "A simple Tumblr photo/video downloader"
            };

            app.HelpOption("-?|-h|--help");

            var mediaOptions = app.Option("-m|--media <kind>",
                "Download PHOTO, VIDEO, AUDIO or ALL media (Default: ALL)",
                CommandOptionType.SingleValue);

            var tagOptions = app.Option("-t|--tag <tag>",
                "Only download photos, videos and/or audios with this tag",
                CommandOptionType.SingleValue);

            var debugOptions = app.Option("-d|--debug",
                "Run the program in debug mode.", CommandOptionType.NoValue);

            var pathOptions = app.Option("-p|--path <path>",
                "The UNC-path to save the media to (Default: \"Downloads\")",
                CommandOptionType.SingleValue);

            var threadsOptions = app.Option("-t|--thread <count>",
                "The maximum number of download threads (1 to CPUs * 4)",
                CommandOptionType.SingleValue);

            var blogName = app.Argument("BlogName",
                "The name of a Tumblr blog that contains photos, videos and/or audios");

            app.OnExecute(async () =>
            {
                if (args.Length == 0)
                    return ExitCode.NoArgs;

                if (string.IsNullOrWhiteSpace(blogName.Value))
                    return ExitCode.NoBlogName;

                try
                {
                    var blogNameRegex = new Regex(@"^[a-z0-9\-]{3,32}$");

                    if (!blogNameRegex.IsMatch(blogName.Value))
                        return ExitCode.BadBlogName;

                    var name = blogName.Value.ToLower();

                    var media = Media.All;

                    if (mediaOptions.HasValue())
                        media = mediaOptions.Value().ToEnum<Media>();

                    var folder = pathOptions.Value();

                    if (!folder.IsFolderName(false))
                        folder = "Downloads";

                    if (!folder.EndsWith('/'))
                        folder += "/";

                    folder += name + "/";

                    folder.EnsurePathExists();

                    int threads = Environment.ProcessorCount;

                    if (threadsOptions.HasValue())
                        threads = int.Parse(threadsOptions.Value());

                    if (threads < 1 || threads >= Environment.ProcessorCount * 4)
                        return ExitCode.BadThreads;

                    var tumblr = new Tumblr(logger);

                    var posts = await tumblr.GetPostsAsync(
                        name, folder, media, debugOptions.HasValue(), threads);

                    await tumblr.FetchAndSaveFilesAsync(
                        folder, posts, debugOptions.HasValue(), threads);

                    return ExitCode.Success;
                }
                catch (Exception error)
                {
                    logger.LogError(error, "An unexpected failure occured.");

                    return ExitCode.RuntimeError;
                }
            });

            try
            {
                var exitCode = app.Execute(args);

                if (exitCode != ExitCode.Success)
                {
                    Console.Write(appInfo.Title);

                    app.ShowHelp();
                }

                return exitCode;
            }
            catch (CommandParsingException error)
            {
                Console.WriteLine();
                Console.WriteLine(error.Message);

                logger.LogWarning(error,
                    "The command-line could not be parsed.");

                return ExitCode.ParseError;
            }
            catch (Exception error)
            {
                logger.LogError(error.Message);

                logger.LogWarning(error, "A runtime error occured.");

                return ExitCode.RuntimeError;
            }
        }
    }
}
