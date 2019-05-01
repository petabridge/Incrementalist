using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Incrementalist.Git;
using Incrementalist.ProjectSystem;
using LibGit2Sharp;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

namespace Incrementalist.Cmd
{

    /// <summary>
    /// Load a VS <see cref="Solution"/> from file and analyze its contents.
    /// </summary>
    public sealed class LoadSolutionCmd : BuildCommandBase
    {
        private readonly MSBuildWorkspace _workspace;
        private readonly Progress<ProjectLoadProgress> _progress;

        public LoadSolutionCmd(ILogger logger, MSBuildWorkspace workspace, CancellationToken token) : base("LoadMsBuildWorkspace", logger, token)
        {
            _workspace = workspace;
            _progress = new Progress<ProjectLoadProgress>(p =>
            {
                Logger.LogInformation("[{0}][{1}] - {2} [{3}]", p.ElapsedTime, p.Operation, p.FilePath, p.TargetFramework);
            });
        }

        protected override async Task<object> ProcessImpl(Task<object> previousTask)
        {
            var slnObject = await previousTask;
            Contract.Assert(slnObject is string fileName && !string.IsNullOrEmpty(fileName), $"Expected previous task to return a " +
                                                                                           $"solution filename. Instead returned {slnObject}");
            var slnName = (string) slnObject;
            Contract.Assert(File.Exists(slnName), $"Expected to find {slnName} on the file system, but couldn't.");

            return await _workspace.OpenSolutionAsync(slnName, _progress, CancellationToken);
        }
    }

    class Program
    {
        private static string _originalTitle = null;
        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private static void SetTitle()
        {
            if (IsWindows) // changing console title is not supported on OS X or Linux
            {
                _originalTitle = Console.Title;
                Console.Title = StartupData.ConsoleWindowTitle;
            }
        }

        private static void ResetTitle()
        {
            if (IsWindows)
                Console.Title = _originalTitle; // reset the console window title back
        }

        static async Task<int> Main(string[] args)
        {
            SetTitle();

            if (args.Length >= 1)
            {
                if (args[0].ToLowerInvariant().Equals("help"))
                {
                    StartupData.ShowHelp();
                    ResetTitle();
                    return 0;
                }
            }

            var insideRepo = Repository.IsValid(Directory.GetCurrentDirectory());
            Console.WriteLine("Are we inside repository? {0}", insideRepo);
            //if (insideRepo)
            //{
            var repoFolder = Repository.Discover(Directory.GetCurrentDirectory());
            var repository = new Repository(repoFolder);            
            Console.WriteLine("Repo base is located in {0}", repoFolder);
            var workingFolder = Directory.GetParent(repoFolder).Parent;

            var affectedFiles = DiffHelper.ChangedFiles(repository, "dev").Select(x => Path.GetFullPath(x, workingFolder.FullName)).ToList();
            foreach (var file in affectedFiles)
                Console.WriteLine("Modified file: {0}", file);
            //}

            // Locate and register the default instance of MSBuild installed on this machine.
            MSBuildLocator.RegisterDefaults();

            var msBuild = MSBuildWorkspace.Create();
            var progress = new Progress<ProjectLoadProgress>();
            progress.ProgressChanged += ProgressOnProgressChanged;

            if (!string.IsNullOrEmpty(repoFolder))
            {
                
                foreach (var sln in SolutionFinder.GetSolutions(workingFolder.FullName))
                {
                    Console.WriteLine("Found solution file {0}", sln);

                    var fullSln = await msBuild.OpenSolutionAsync(sln, progress);
                    var allDocuments = fullSln.Projects.SelectMany(x => x.Documents)
                        .GroupBy(x => x.FilePath, document => FileType.Code)
                        .ToDictionary(x => Path.GetFullPath(x.Key, workingFolder.FullName), x => x.First())
                        .Concat(fullSln.Projects.ToDictionary(x => Path.GetFullPath(x.FilePath, workingFolder.FullName), x => FileType.Project))
                        .Concat(new Dictionary<string, FileType>{ { Path.GetFullPath(fullSln.FilePath, workingFolder.FullName), FileType.Solution } })
                        .ToDictionary(x => x.Key, x => x.Value);

                    Console.WriteLine("Checking to see if affected files are in solution...");
                    foreach (var affectedFile in affectedFiles)
                    {
                        var fileInSln = allDocuments.ContainsKey(affectedFile);
                        Console.WriteLine("{0} in solution? {1}", affectedFile, fileInSln);
                    }
                }
            }

            ResetTitle();
            return 0;
        }

        private static void ProgressOnProgressChanged(object sender, ProjectLoadProgress e)
        {
            Console.WriteLine("{0} {1} {2}", e.ElapsedTime, e.Operation, e.FilePath);
        }
    }
}
