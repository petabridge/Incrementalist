using System.IO;
using System.Linq;
using Xunit;

namespace Incrementalist.Tests.Helpers.Tests;

public class SolutionBuilderSpecs
{
    [Fact]
    public void ShouldBuildBasicSolution()
    {
        // var arrange
        var slnName = "TestSolution";

        // act
        var solution = new TestSolutionBuilder(slnName)
            .AddFolder("src", folderBuilder =>
            {
                folderBuilder.AddProject("ProjectA",
                    (otherProjects, projBuilder) =>
                    {
                        projBuilder.WithFile("FileA.cs", "using System; public class FileA { }");
                    });

                folderBuilder.AddProject("ProjectB", (otherProjects, projBuilder) =>
                {
                    projBuilder.WithFile("FileB.cs", "using System; public class FileB { }");
                    // get a reference to ProjectA
                    var projectA = otherProjects.First(p => p.NameWithoutExtension == "ProjectA");
                    projBuilder.WithProjectReference(projectA);
                });
            })
            .AddProject("BuildProject", (otherProjects, projBuilder) =>
            {
                projBuilder.WithFile("BuildFile.cs", "using System; public class BuildFile { }");
            }).Build();

        var serializedSolution = solution.Serialize();
        
        // assert
        Assert.Equal("TestSolution", solution.Name);
        Assert.Equal("TestSolution.slnx", solution.FileName.Name);
        Assert.Equal(3, solution.FlatProjects.Count);
        
        var expectedProjectNames = new[]
        {
            Path.Join("src", "ProjectA" , "ProjectA.csproj"),
            Path.Join("src", "ProjectB" , "ProjectB.csproj"),
            Path.Join( "BuildProject" , "BuildProject.csproj"),
        };
        
        Assert.Contains(expectedProjectNames[0], serializedSolution);
        Assert.Contains(expectedProjectNames[1], serializedSolution);
        Assert.Contains(expectedProjectNames[2], serializedSolution);
    }
}