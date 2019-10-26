// -----------------------------------------------------------------------
// <copyright file="LoadSolutionCmd.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

namespace Incrementalist.Cmd
{
    /// <summary>
    ///     Load a VS <see cref="Solution" /> from file and analyze its contents.
    /// </summary>
    public sealed class LoadSolutionCmd : BuildCommandBase<string, Solution>
    {
        private readonly Progress<ProjectLoadProgress> _progress;
        private readonly MSBuildWorkspace _workspace;

        public LoadSolutionCmd(ILogger logger, MSBuildWorkspace workspace, CancellationToken token) : base(
            "LoadMsBuildWorkspace", logger, token)
        {
            _workspace = workspace;
            _progress = new Progress<ProjectLoadProgress>(p =>
            {
                Logger.LogTrace("[{0}][{1}] - {2} [{3}]", p.ElapsedTime, p.Operation, p.FilePath,
                    p.TargetFramework);
            });
        }

        protected override async Task<Solution> ProcessImpl(Task<string> previousTask)
        {
            var slnObject = await previousTask;
            Contract.Assert(slnObject is string fileName && !string.IsNullOrEmpty(fileName),
                "Expected previous task to return a " +
                $"solution filename. Instead returned {slnObject}");
            var slnName = slnObject;
            Contract.Assert(File.Exists(slnName), $"Expected to find {slnName} on the file system, but couldn't.");
            
            // Log any solution loading issues
            _workspace.WorkspaceFailed += (sender, args) =>
            {
                var message = $"Issue during solution loading: {sender}: {args.Diagnostic.Message}";
                var logLevel = args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure ? LogLevel.Error : LogLevel.Warning;
                
                Logger.Log(logLevel, message);
            };

            // Roslyn does not support FSharp projects, but .fsproj has same structure as .csproj files,
            // so can treat them as a known project type to support diff tracking
            _workspace.AssociateFileExtensionWithLanguage("fsproj", LanguageNames.CSharp);
            
            return await _workspace.OpenSolutionAsync(slnName, _progress, CancellationToken);
        }
    }
}