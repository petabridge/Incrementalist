// -----------------------------------------------------------------------
// <copyright file="ProjectBuilderSpecs.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Incrementalist.Tests.Helpers.Tests;

public class ProjectBuilderSpecs
{
    [Fact]
    public void ShouldBuildBasicProject()
    {
        // arrange
        var guid = new Guid("00000000-0000-0000-0000-000000000001");
        var projectId = ProjectId.CreateFromSerialized(guid);
        var projectPath = @"C:\MyProject\";
        var projectName = "MyProject";

        // act
        var projectBuilder = new ProjectBuilder(projectId, projectPath, projectName);

        var project = projectBuilder
            .Build();

        var serialized = project.Serialize();
        var targetFrameworks = project.TargetFrameworks.Serialize();
        var outputType = project.ProjectProperties
            .Single(c => c.PropertyType == PropertyType.OutputType).Serialize();

        // assert
        var expectedProjectPath = Path.Combine(projectPath, $"{projectName}.csproj");

        Assert.Equal(projectId, project.ProjectId);
        Assert.Equal(expectedProjectPath, project.CompletePath);
        Assert.Contains(targetFrameworks, serialized);
        Assert.Contains(outputType, serialized);
    }

    [Theory]
    [InlineData(ProjectLanguage.CSharp, OutputType.Exe)]
    [InlineData(ProjectLanguage.CSharp, OutputType.Library)]
    [InlineData(ProjectLanguage.FSharp, OutputType.Exe)]
    [InlineData(ProjectLanguage.FSharp, OutputType.Library)]
    public void ShouldBuildProject(ProjectLanguage language, OutputType outputType)
    {
        // arrange
        var guid = new Guid("00000000-0000-0000-0000-000000000001");
        var projectId = ProjectId.CreateFromSerialized(guid);
        var projectPath = @"C:\MyProject\";
        var projectName = "MyProject";

        // act
        var projectBuilder = new ProjectBuilder(projectId, projectPath, projectName);
        var project = projectBuilder
            .WithProjectType(outputType)
            .WithLanguage(language)
            .Build();

        var serialized = project.Serialize();

        var targetFrameworks = project.TargetFrameworks.Serialize();
        var outputTypeSerialized = project.ProjectProperties
            .Single(c => c.PropertyType == PropertyType.OutputType).Serialize();

        // assert
        var expectedFileExtension = language == ProjectLanguage.CSharp ? ".csproj" : ".fsproj";
        var expectedProjectPath = Path.Combine(projectPath, $"{projectName}{expectedFileExtension}");

        Assert.Equal(projectId, project.ProjectId);
        Assert.Equal(expectedProjectPath, project.CompletePath);
        Assert.Contains(targetFrameworks, serialized);
        Assert.Contains(outputTypeSerialized, serialized);
    }

    [Fact]
    public void ShouldBuildProjectWithDependencies()
    {
        // need to create two projects, A and B - B depends on A
        // arrange
        var guidA = new Guid("00000000-0000-0000-0000-000000000001");
        var projectIdA = ProjectId.CreateFromSerialized(guidA);
        var projectPathA = @"src/MyProjectA/";
        var projectNameA = "MyProjectA";

        var guidB = new Guid("00000000-0000-0000-0000-000000000002");
        var projectIdB = ProjectId.CreateFromSerialized(guidB);
        var projectPathB = @"src/MyProjectB/";
        var projectNameB = "MyProjectB";

        // act
        var projectBuilderA = new ProjectBuilder(projectIdA, projectPathA, projectNameA);
        var projectA = projectBuilderA
            .WithProjectType(OutputType.Library)
            .WithLanguage(ProjectLanguage.CSharp)
            .Build();

        var projectBuilderB = new ProjectBuilder(projectIdB, projectPathB, projectNameB);
        var projectB = projectBuilderB
            .WithProjectType(OutputType.Library)
            .WithLanguage(ProjectLanguage.CSharp)
            .WithProjectReference(projectA)
            .Build();

        var serializedB = projectB.Serialize();

        // assert
        // Need to validate that there is a ProjectReference that uses the path "../MyProjectA/MyProjectA.csproj"
        Assert.Contains("ProjectReference Include=\"..\\MyProjectA\\MyProjectA.csproj\"", serializedB);
    }
}