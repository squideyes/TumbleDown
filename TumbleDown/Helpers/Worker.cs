﻿using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using System;

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
                    // TODO: validate blog-name

                    var media = Media.All;

                    if (mediaOptions.HasValue())
                        media = mediaOptions.Value().ToEnum<Media>();

                    var folder = pathOptions.Value();

                    if (!folder.IsFolderName(false))
                        folder = "Downloads";

                    if (!folder.EndsWith('/'))
                        folder += "/";

                    folder += blogName.Value + "/";

                    folder.EnsurePathExists();

                    int threads = 4;

                    if(threadsOptions.HasValue())
                        threads = int.Parse(threadsOptions.Value());

                    if (threads < 1 || threads >= Environment.ProcessorCount * 4)
                        return ExitCode.BadThreads;

                    var tumblr = new Tumblr(logger);

                    var posts = await tumblr.GetPostsAsync(
                        blogName.Value, folder, media, debugOptions.HasValue(), threads);

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
