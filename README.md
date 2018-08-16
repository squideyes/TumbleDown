TumbleDown is a simple command-line app that bulk-downloads all of the photos and/or video files from a particular Tumblr blog, with the option to limit the downloads by media and/or tag.

To run TumblrDown issue a command like: **TumbleDown** <**BlogName**> [**Options**]

|Kind|Value|Notes|
|---|---|---|
|Argument|BlogName|The name of a Tumblr blog  that contains photos and/or videos you wish to download (ie. bad-postcards, lolvideoslol, 50watts, watchinglifehappen, etc.)|
|Option|-m {media}|Download PHOTO, VIDEO or ALL media (Default: ALL)|
|Option|-t {tag}|Only download photos and/or videos with this tag|
|Option|-p {path}|The UNC-path to save the media to (Default: "Downloads")|

**NOTE #1:** TumbleDown is NOT a comprehensive backup solution.  As such, it doesn't save text-posts, for instance, nor does it save most of the interesting metadata associated with posts

**NOTE #2**: you should only download media that is in the public domain or that you have explicit rights to (i.e. your own content).

**NOTE #3**: you may want to package the program as an EXE.  If so open a command prompt, navigate to the solution folder then issue a command like the following: **dotnet publish -c Release -r win10-x86**