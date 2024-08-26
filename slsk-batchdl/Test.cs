﻿using Data;
using Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using static Test.Helpers;

namespace Test
{
    static class Test
    {
        public static async Task RunAllTests()
        {
            TestStringUtils();
            TestAutoProfiles();
            TestProfileConditions();
            await TestStringExtractor();
            TestM3uEditor();

            Console.WriteLine('\n' + new string('#', 50) + '\n' + "All tests passed.");
        }

        public static void TestStringUtils()
        {
            SetCurrentTest("TestStringUtils");

            // RemoveFt
            Assert("blah blah ft. blah blah"    .RemoveFt() == "blah blah");
            Assert("blah blah feat. blah blah"  .RemoveFt() == "blah blah");
            Assert("blah (feat. blah blah) blah".RemoveFt() == "blah blah");
            Assert("blah (ft. blah blah) blah"  .RemoveFt() == "blah blah");

            // RemoveConsecutiveWs
            Assert(" blah    blah  blah blah ".RemoveConsecutiveWs() == " blah blah blah blah ");

            // RemoveSquareBrackets
            Assert("foo [aaa] bar".RemoveSquareBrackets() == "foo  bar");

            // ReplaceInvalidChars
            Assert("Invalid chars: \\/:|?<>*\"".ReplaceInvalidChars("", true) == "Invalid chars ");
            Assert("Invalid chars: \\/:|?<>*\"".ReplaceInvalidChars("", true, false) == "Invalid chars \\/");

            // ContainsWithBoundary
            Assert("foo blah bar".ContainsWithBoundary("blah"));
            Assert("foo/blah/bar".ContainsWithBoundary("blah"));
            Assert("foo - blah 2".ContainsWithBoundary("blah"));
            Assert(!"foo blah bar".ContainsWithBoundaryIgnoreWs("blah"));
            Assert(!"foo - blah 2".ContainsWithBoundaryIgnoreWs("blah"));
            Assert("foo - blah 2 - bar".ContainsWithBoundaryIgnoreWs("blah 2"));
            Assert("foo/blah/bar".ContainsWithBoundaryIgnoreWs("blah"));
            Assert("01 blah".ContainsWithBoundaryIgnoreWs("blah", acceptLeftDigit: true));
            Assert(!"foo - blah 2blah".ContainsWithBoundaryIgnoreWs("blah", acceptLeftDigit: true));
            Assert("foo - blah 2 blah".ContainsWithBoundaryIgnoreWs("blah", acceptLeftDigit: true));

            // GreatestCommonPath
            var paths = new string[]
            {
                "/home/user/docs/nested/file",
                "/home/user/docs/nested/folder/",
                "/home/user/docs/letter.txt",
                "/home/user/docs/report.pdf",
                "/home/user/docs/",
            };
            Assert(Utils.GreatestCommonPath(paths, dirsep: '/') == "/home/user/docs/");
            Assert(Utils.GreatestCommonPath(new string[] { "/path/file", "" }, dirsep: '/') == "");
            Assert(Utils.GreatestCommonPath(new string[] { "/path/file", "/" }, dirsep: '/') == "/");
            Assert(Utils.GreatestCommonPath(new string[] { "/path/dir1", "/path/dir2" }, dirsep: '/') == "/path/");
            Assert(Utils.GreatestCommonPath(new string[] { "/path/dir1", "/path/dir2" }, dirsep: '\\') == "");
            Assert(Utils.GreatestCommonPath(new string[] { "dir1", "dir2" }, dirsep: '/') == "");

            // RemoveDiacritics
            Assert(" Café Crème à la mode Ü".RemoveDiacritics() == " Cafe Creme a la mode U");

            Passed();
        }

        public static void TestAutoProfiles()
        {
            SetCurrentTest("TestAutoProfiles");

            ResetProfiles(); 
            Config.inputType = InputType.YouTube;
            Config.interactiveMode = true;
            Config.album = true;
            Config.aggregate = false;
            Config.maxStaleTime = 500000;

            string path = Path.Join(Directory.GetCurrentDirectory(), "test_conf.conf");
            Config.confPath = path;

            string content =  
                "max-stale-time = 5" +
                "\nfast-search = true" +
                "\nformat = flac" +

                "\n[profile-true-1]" +
                "\nprofile-cond = input-type == \"youtube\" && download-mode == \"album\"" +
                "\nmax-stale-time = 10" +

                "\n[profile-true-2]" +
                "\nprofile-cond = !aggregate" +
                "\nfast-search = false" +

                "\n[profile-false-1]" +
                "\nprofile-cond = input-type == \"string\"" +
                "\nformat = mp3" +

                "\n[profile-no-cond]" +
                "\nformat = opus";

            File.WriteAllText(path, content);

            Config.ParseArgsAndReadConfig(new string[] { });

            //Config.PostProcessArgs();

            Assert(Config.maxStaleTime == 10 && !Config.fastSearch && Config.necessaryCond.Formats[0] == "flac");

            ResetProfiles();
            Config.inputType = InputType.CSV;
            Config.album = true;
            Config.interactiveMode = true;
            Config.useYtdlp = false;
            Config.maxStaleTime = 50000;
            content = 
                "\n[no-stale]" +
                "\nprofile-cond = interactive && download-mode == \"album\"" +
                "\nmax-stale-time = 999999" +
                "\n[youtube]" +
                "\nprofile-cond = input-type == \"youtube\"" +
                "\nyt-dlp = true";

            File.WriteAllText(path, content);

            Config.ParseArgsAndReadConfig(new string[] { });

            Assert(Config.maxStaleTime == 999999 && !Config.useYtdlp);

            ResetProfiles();
            Config.inputType = InputType.YouTube;
            Config.album = false;
            Config.interactiveMode = true;
            Config.useYtdlp = false;
            Config.maxStaleTime = 50000;
            content =
                "\n[no-stale]" +
                "\nprofile-cond = interactive && download-mode == \"album\"" +
                "\nmax-stale-time = 999999" +
                "\n[youtube]" +
                "\nprofile-cond = input-type == \"youtube\"" +
                "\nyt-dlp = true";

            File.WriteAllText(path, content);

            Config.ParseArgsAndReadConfig(new string[] { });

            Assert(Config.maxStaleTime == 50000 && Config.useYtdlp);

            if (File.Exists(path))
                File.Delete(path);

            Passed();
        }

        public static void TestProfileConditions()
        {
            SetCurrentTest("TestProfileConditions");

            Config.inputType = InputType.YouTube;
            Config.interactiveMode = true;
            Config.album = true;
            Config.aggregate = false;

            var conds = new (bool, string)[] 
            {
                (false, "aggregate"),
                (true,  "interactive"),
                (true,  "album"),
                (false, "!interactive"),
                (true,  "album && input-type == \"youtube\""),
                (false, "album && input-type != \"youtube\""),
                (false, "(interactive && aggregate)"),
                (true,  "album && (interactive || aggregate)"),
                (true,  "input-type == \"spotify\" || aggregate || input-type == \"csv\" || interactive && album"),
                (true,  "    input-type!=\"youtube\"||(album&&!interactive  ||(aggregate    ||    interactive  )  )"),
                (false, "    input-type!=\"youtube\"||(album&&!interactive  ||(aggregate    ||    !interactive  )  )"),
            };

            foreach ((var b, var c) in conds)
            {
                Console.WriteLine(c);
                Assert(b == Config.ProfileConditionSatisfied(c));
            }

            Passed();
        }

        public static async Task TestStringExtractor()
        {
            SetCurrentTest("TestStringExtractor");

            var strings = new List<string>()
            {
                "Some Title",
                "Some, Title",
                "artist = Some artist, title = some title",
                "Artist - Title, length = 42",
                "title=Some, Title, artist=Some, Artist, album = Some, Album, length= 42",
                "Some, Artist = a - Some, Title = b, album = Some, Album, length = 42",

                "Foo Bar",
                "Foo - Bar",
                "Artist - Title, length=42",
                "title=Title, artist=Artist, length=42",
            };

            var tracks = new List<Track>()
            {
                new Track() { Title="Some Title" },
                new Track() { Title="Some, Title" },
                new Track() { Title = "some title", Artist = "Some artist" },
                new Track() { Title = "Title", Artist = "Artist", Length = 42 },
                new Track() { Title="Some, Title", Artist = "Some, Artist", Album = "Some, Album", Length = 42 },
                new Track() { Title="Some, Title = b", Artist = "Some, Artist = a", Album = "Some, Album", Length = 42 },

                new Track() { Title = "Foo Bar" },
                new Track() { Title = "Bar", Artist = "Foo" },
                new Track() { Title = "Title", Artist = "Artist", Length = 42 },
                new Track() { Title = "Title", Artist = "Artist", Length = 42 },
            };

            var albums = new List<Track>()
            {
                new Track() { Album="Some Title" },
                new Track() { Album="Some, Title" },
                new Track() { Title = "some title", Artist = "Some artist" },
                new Track() { Album = "Title", Artist = "Artist", Length = 42 },
                new Track() { Title="Some, Title", Artist = "Some, Artist", Album = "Some, Album", Length = 42 },
                new Track() { Artist = "Some, Artist = a", Album = "Some, Album", Length = 42 },

                new Track() { Album = "Foo Bar" },
                new Track() { Album = "Bar", Artist = "Foo" },
                new Track() { Album = "Title", Artist = "Artist", Length = 42 },
                new Track() { Title = "Title", Artist = "Artist", Length = 42 },
            };

            var extractor = new Extractors.StringExtractor();

            Config.aggregate = false;
            Config.album = false;

            Console.WriteLine("Testing songs: ");
            for (int i = 0; i < strings.Count; i++)
            {
                Config.input = strings[i];
                Console.WriteLine(Config.input);
                var res = await extractor.GetTracks();
                var t = res[0].list[0][0];
                Assert(Extractors.StringExtractor.InputMatches(Config.input));
                Assert(t.ToKey() == tracks[i].ToKey());
            }

            Console.WriteLine();
            Console.WriteLine("Testing albums");
            Config.album = true;
            for (int i = 0; i < strings.Count; i++)
            {
                Config.input = strings[i];
                Console.WriteLine(Config.input);
                var t = (await extractor.GetTracks())[0].source;
                Assert(Extractors.StringExtractor.InputMatches(Config.input));
                Assert(t.ToKey() == albums[i].ToKey());
            }

            Passed();
        }

        public static void TestM3uEditor()
        {
            SetCurrentTest("TestM3uEditor");

            Config.m3uOption = M3uOption.All;
            Config.skipMode = SkipMode.M3u;
            Config.printOption = PrintOption.Tracks | PrintOption.Full;
            Config.skipExisting = true;

            string path = Path.Join(Directory.GetCurrentDirectory(), "test_m3u.m3u8");

            if (File.Exists(path))
                File.Delete(path);

            File.WriteAllText(path, $"#SLDL:" +
                $"{Path.Join(Directory.GetCurrentDirectory(), "file1.5")},\"Artist, 1.5\",,\"Title, , 1.5\",-1,3,0;" +
                $"path/to/file1,\"Artist, 1\",,\"Title, , 1\",-1,3,0;" +
                $"path/to/file2,\"Artist, 2\",,Title2,-1,3,0;,\"Artist; ,3\",,Title3 ;a,-1,4,0;" +
                $",\"Artist,,, ;4\",,Title4,-1,4,3;" +
                $",,,,-1,0,0;");

            var notFoundInitial = new List<Track>()
            {
                new() { Artist = "Artist; ,3", Title = "Title3 ;a" },
                new() { Artist = "Artist,,, ;4", Title = "Title4", State = TrackState.Failed, FailureReason = FailureReason.NoSuitableFileFound }
            };
            var existingInitial = new List<Track>()
            {
                new() { Artist = "Artist, 1", Title = "Title, , 1", DownloadPath = "path/to/file1", State = TrackState.Downloaded },
                new() { Artist = "Artist, 1.5", Title = "Title, , 1.5", DownloadPath = Path.Join(Directory.GetCurrentDirectory(), "file1.5"), State = TrackState.Downloaded },
                new() { Artist = "Artist, 2", Title = "Title2", DownloadPath = "path/to/file2", State = TrackState.AlreadyExists }
            };
            var toBeDownloadedInitial = new List<Track>()
            {
                new() { Artist = "ArtistA", Album = "Albumm", Title = "TitleA" },
                new() { Artist = "ArtistB", Album = "Albumm", Title = "TitleB" }
            };

            var trackLists = new TrackLists();
            trackLists.AddEntry();
            foreach (var t in notFoundInitial)
                trackLists.AddTrackToLast(t);
            foreach (var t in existingInitial)
                trackLists.AddTrackToLast(t);
            foreach (var t in toBeDownloadedInitial)
                trackLists.AddTrackToLast(t);

            Program.m3uEditor = new M3uEditor(path, trackLists, Config.m3uOption);

            var notFound = (List<Track>)ProgramInvoke("DoSkipNotFound", new object[] { trackLists[0].list[0] });
            var existing = (List<Track>)ProgramInvoke("DoSkipExisting", new object[] { trackLists[0].list[0], false });
            var toBeDownloaded = trackLists[0].list[0].Where(t => t.State == TrackState.Initial).ToList();

            Assert(notFound.SequenceEqualUpToPermutation(notFoundInitial));
            Assert(existing.SequenceEqualUpToPermutation(existingInitial));
            Assert(toBeDownloaded.SequenceEqualUpToPermutation(toBeDownloadedInitial));

            ProgramInvoke("PrintTracksTbd", new object[] { toBeDownloaded, existing, notFound, ListType.Normal });

            Program.m3uEditor.Update();
            string output = File.ReadAllText(path);
            string need = 
                "#SLDL:./file1.5,\"Artist, 1.5\",,\"Title, , 1.5\",-1,3,0;path/to/file1,\"Artist, 1\",,\"Title, , 1\",-1,3,0;path/to/file2,\"Artist, 2\",,Title2,-1,3,0;,\"Artist; ,3\",,Title3 ;a,-1,4,0;,\"Artist,,, ;4\",,Title4,-1,4,3;,,,,-1,0,0;" +
                "\n" +
                "\n# Failed: Artist; ,3 - Title3 ;a [NoSuitableFileFound]" +
                "\n# Failed: Artist,,, ;4 - Title4 [NoSuitableFileFound]" +
                "\npath/to/file1" +
                "\nfile1.5" +
                "\npath/to/file2" +
                "\n";
            Assert(output == need);

            toBeDownloaded[0].State = TrackState.Downloaded;
            toBeDownloaded[0].DownloadPath = "new/file/path";
            toBeDownloaded[1].State = TrackState.Failed;
            toBeDownloaded[1].FailureReason = FailureReason.NoSuitableFileFound;
            existing[1].DownloadPath = "/other/new/file/path";

            Program.m3uEditor.Update();
            output = File.ReadAllText(path);
            need = 
                "#SLDL:/other/new/file/path,\"Artist, 1.5\",,\"Title, , 1.5\",-1,3,0;path/to/file1,\"Artist, 1\",,\"Title, , 1\",-1,3,0;path/to/file2,\"Artist, 2\",,Title2,-1,3,0;,\"Artist; ,3\",,Title3 ;a,-1,4,0;,\"Artist,,, ;4\",,Title4,-1,4,3;" +
                ",,,,-1,0,0;new/file/path,ArtistA,Albumm,TitleA,-1,1,0;,ArtistB,Albumm,TitleB,-1,2,3;" +
                "\n" +
                "\n# Failed: Artist; ,3 - Title3 ;a [NoSuitableFileFound]" +
                "\n# Failed: Artist,,, ;4 - Title4 [NoSuitableFileFound]" +
                "\npath/to/file1" +
                "\n/other/new/file/path" +
                "\npath/to/file2" +
                "\nnew/file/path" +
                "\n# Failed: ArtistB - TitleB [NoSuitableFileFound]" +
                "\n";
            Assert(output == need);

            Console.WriteLine();
            Console.WriteLine(output);

            Program.m3uEditor = new M3uEditor(path, trackLists, Config.m3uOption);

            foreach (var t in trackLists.Flattened(false, false))
            {
                Program.m3uEditor.TryGetPreviousRunResult(t, out var prev);
                Assert(prev != null);
                Assert(prev.ToKey() == t.ToKey());
                Assert(prev.DownloadPath == t.DownloadPath);
                Assert(prev.State == t.State || prev.State == TrackState.NotFoundLastTime);
                Assert(prev.FailureReason == t.FailureReason);
            }

            Program.m3uEditor.Update();
            output = File.ReadAllText(path);
            Assert(output == need);

            File.Delete(path);

            Passed();
        }
    }

    static class Helpers
    {
        static string? currentTest = null;

        public static void SetCurrentTest(string name)
        {
            currentTest = name;
        }

        public static object? ProgramInvoke(string name, object[] parameters)
        {
            var method = typeof(Program).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            return method.Invoke(null, parameters);
        }

        public class AssertionError : Exception
        {
            public AssertionError() : base("Assertion failed.") { }
            public AssertionError(string message) : base(message) { }
        }

        public static void Assert(bool condition, string message = "Assertion failed")
        {
            if (!condition)
            {
                var stackTrace = new StackTrace(true);
                var frame = stackTrace.GetFrame(1);
                var fileName = frame.GetFileName();
                var lineNumber = frame.GetFileLineNumber();
                throw new AssertionError($"{currentTest}: {message} (at {fileName}:{lineNumber})");
            }
        }

        public static void ResetProfiles()
        {
            var type = typeof(Config);
            var field = type.GetField("profiles", BindingFlags.NonPublic | BindingFlags.Static);
            var value = (Dictionary<string, (List<string> args, string? cond)>)field.GetValue(null);
            value.Clear();
        }

        public static void Passed()
        {
            Console.WriteLine($"{currentTest} passed");
            currentTest = null;
        }
    }
}
