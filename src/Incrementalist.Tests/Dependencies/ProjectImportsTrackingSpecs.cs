using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Incrementalist.Git;
using Incrementalist.ProjectSystem;
using Incrementalist.ProjectSystem.Cmds;
using Incrementalist.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;

namespace Incrementalist.Tests.Dependencies
{
    public class ProjectImportsTrackingSpecs : IDisposable
    {
        private readonly ITestOutputHelper _outputHelper;
        public DisposableRepository Repository { get; }
        
        public ProjectImportsTrackingSpecs(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
            Repository = new DisposableRepository();
        }

        public void Dispose()
        {
            Repository?.Dispose();
        }

        [Fact(DisplayName = "List of project imported files should be loaded correctly")]
        public void ImportedFilePathIsFound()
        {
            var sample = ProjectSampleGenerator.GetProjectWithImportSample("SampleProject.csproj");
            var projectFilePath = sample.ProjectFile.GetFullPath(Repository.BasePath);
            var importedPropsFilePath = Path.Combine(Repository.BasePath, sample.ImportedPropsFile.Name);
            
            Repository
                .WriteFile(sample.ProjectFile)
                .WriteFile(sample.ImportedPropsFile);

            var projectFile = new SlnFileWithPath(projectFilePath, new SlnFile(FileType.Project, ProjectId.CreateNewId())) ;
            var imports = ProjectImportsFinder.FindProjectImports(new[] { projectFile });
            
            imports.Values.Should().BeEquivalentTo(new ImportedFile(importedPropsFilePath, new[] { projectFile }.ToImmutableList()));
        }

        [Fact(DisplayName = "When project imported file is changed, the project should be marked as affected")]
        public async Task Should_mark_project_as_changed_when_only_imported_file_changed()
        {
            var sample = ProjectSampleGenerator.GetProjectWithImportSample("SampleProject.csproj");
            var projectFilePath = sample.ProjectFile.GetFullPath(Repository.BasePath);
            
            Repository
                .WriteFile(sample.ProjectFile)
                .WriteFile(sample.ImportedPropsFile)
                .Commit("Created sample project")
                .CreateBranch("foo")
                .CheckoutBranch("foo")
                .WriteFile(sample.ImportedPropsFile.Name, sample.ImportedPropsFile.Content + " ")
                .Commit("Updated imported file with a space");

            var cmd = new FilterAffectedProjectFilesCmd(new TestOutputLogger(_outputHelper), new CancellationToken(), Repository.BasePath, "master");
            var solutionFiles = new Dictionary<string, SlnFile>()
            {
                [projectFilePath] = new SlnFile(FileType.Project, ProjectId.CreateNewId())
            };
            var filteredAffectedFiles = await cmd.Process(Task.FromResult(solutionFiles));
            filteredAffectedFiles.Should().HaveCount(1).And.Subject.Keys.Should().BeEquivalentTo(projectFilePath);
        }
    }
}