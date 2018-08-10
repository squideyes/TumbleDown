using Microsoft.Extensions.CommandLineUtils;
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
                "Only download photos and/or videos with this tag",
                CommandOptionType.SingleValue);

            var pathOptions = app.Option("-p|--path <path>",
                "The UNC-path to save the media to (Default: \"Downloads\")",
                CommandOptionType.SingleValue);

            var blogName = app.Argument("BlogName",
                "The name of a Tumblr blog that contains photos, videos and/or audios");

            app.OnExecute(async () =>
            {
                if (args.Length == 0)
                    return ExitCode.NoArgs;

                if (string.IsNullOrWhiteSpace(blogName.Value))
                    return ExitCode.NoBlogName;

                // validate blog-name

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

                var tumblr = new Tumblr(logger);

                var posts = await tumblr.GetPostsAsync(
                    blogName.Value, folder, media);

                await tumblr.FetchAndSaveFilesAsync(folder, posts);

                return ExitCode.Success;
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
