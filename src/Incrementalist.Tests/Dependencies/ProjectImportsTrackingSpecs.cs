using System;
using System.Collections.Generic;
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
            Repository
                .WriteFile("SampleProject.csproj", ProjectSampleGenerator.GetProjectFileContent())
                .WriteFile("imported.props", ProjectSampleGenerator.GetImportedPropsFileContent());

            var projectFile = new SlnFileWithPath(Path.Combine(Repository.BasePath, "SampleProject.csproj"), new SlnFile(FileType.Project, ProjectId.CreateNewId())) ;
            var imports = ProjectImportsFinder.FindProjectImports(new[] { projectFile });
            
            imports.Values.Should().BeEquivalentTo(new ImportedFile(Path.Combine(Repository.BasePath, "imported.props"), new[] { projectFile }.ToList()));
        }

        [Fact(DisplayName = "When project imported file is changed, the project should be marked as affected")]
        public async Task Should_mark_project_as_changed_when_only_imported_file_changed()
        {
            Repository
                .WriteFile("SampleProject.csproj", ProjectSampleGenerator.GetProjectFileContent())
                .WriteFile("imported.props", ProjectSampleGenerator.GetImportedPropsFileContent())
                .Commit("Created sample project")
                .CreateBranch("foo")
                .CheckoutBranch("foo")
                .WriteFile("imported.props", ProjectSampleGenerator.GetImportedPropsFileContent() + " ")
                .Commit("Updated imported file with a space");

            var cmd = new FilterAffectedProjectFilesCmd(new TestOutputLogger(_outputHelper), new CancellationToken(), Repository.BasePath, "master");
            var projectFilePath = Path.Combine(Repository.BasePath, "SampleProject.csproj");
            var solutionFiles = new Dictionary<string, SlnFile>()
            {
                [projectFilePath] = new SlnFile(FileType.Project, ProjectId.CreateNewId())
            };
            var filteredAffectedFiles = await cmd.Process(Task.FromResult(solutionFiles));
            filteredAffectedFiles.Should().HaveCount(1).And.Subject.Keys.Should().BeEquivalentTo(projectFilePath);
        }
    }
}