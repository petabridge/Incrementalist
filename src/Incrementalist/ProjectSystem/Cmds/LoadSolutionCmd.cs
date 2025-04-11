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
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace Incrementalist.ProjectSystem.Cmds
{
    public sealed record SolutionDetails(string SolutionFilePath, SolutionModel SolutionModel);
    
    /// <summary>
    ///     Load a VS <see cref="Solution" /> from file and analyze its contents.
    /// </summary>
    public sealed class LoadSolutionCmd : BuildCommandBase<string, SolutionDetails>
    {
        public LoadSolutionCmd(ILogger logger, CancellationToken token) : base(
            "LoadMsBuildWorkspace", logger, token)
        {
            // _workspace = workspace;
            // _progress = new Progress<ProjectLoadProgress>(p =>
            // {
            //     Logger.LogTrace("[{0}][{1}] - {2} [{3}]", p.ElapsedTime, p.Operation, p.FilePath,
            //         p.TargetFramework);
            // });
        }

        protected override async Task<SolutionDetails> ProcessImpl(Task<string> previousTask)
        {
            var slnObject = await previousTask;
            Contract.Assert(slnObject != null && !string.IsNullOrEmpty(slnObject),
                "Expected previous task to return a " +
                $"solution filename. Instead returned {slnObject}");
            Contract.Assert(File.Exists(slnObject), $"Expected to find {slnObject} on the file system, but couldn't.");

            var solutionSerializer = SolutionSerializers.GetSerializerByMoniker(slnObject);
            if (solutionSerializer == null)
            {
                Logger.LogError("{SlnFile} is not a valid solution file", slnObject);
                throw new InvalidOperationException("Invalid solution file: " + slnObject);
            }
            
            return new SolutionDetails(slnObject, await solutionSerializer.OpenAsync(slnObject, CancellationToken));
            
            // // Log any solution loading issues
            // _workspace.WorkspaceFailed += (sender, args) =>
            // {
            //     var message = $"Issue during solution loading: {sender}: {args.Diagnostic.Message}";
            //     var logLevel = args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure ? LogLevel.Error : LogLevel.Warning;
            //     
            //     Logger.Log(logLevel, message);
            // };
            //
            // // Roslyn does not support FSharp projects, but .fsproj has same structure as .csproj files,
            // // so can treat them as a known project type to support diff tracking
            // _workspace.AssociateFileExtensionWithLanguage("fsproj", LanguageNames.CSharp);
            
            //return await _workspace.OpenSolutionAsync(slnObject, _progress, CancellationToken);
        }
    }
}