using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Incrementalist.ProjectSystem.Cache;
using Xunit;

namespace Incrementalist.Tests.Cache
{
    public class DependencyGraphCacheTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _solutionPath;
        private readonly string _project1Path;
        private readonly string _project2Path;
        private readonly string _project3Path;

        public DependencyGraphCacheTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);

            _solutionPath = Path.Combine(_tempDir, "test.sln");
            _project1Path = Path.Combine(_tempDir, "Project1", "Project1.csproj");
            _project2Path = Path.Combine(_tempDir, "Project2", "Project2.csproj");
            _project3Path = Path.Combine(_tempDir, "Project3", "Project3.csproj");

            // Create test files
            Directory.CreateDirectory(Path.GetDirectoryName(_project1Path));
            Directory.CreateDirectory(Path.GetDirectoryName(_project2Path));
            Directory.CreateDirectory(Path.GetDirectoryName(_project3Path));
            File.WriteAllText(_solutionPath, "dummy solution content");
            File.WriteAllText(_project1Path, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            File.WriteAllText(_project2Path, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            File.WriteAllText(_project3Path, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Fact]
        public void Cache_ShouldBeCreatedAndLoaded()
        {
            // Arrange
            var projectFiles = new[] { _project1Path, _project2Path, _project3Path };
            var cache = new DependencyGraphCache
            {
                SolutionPath = _solutionPath,
                Projects = new Dictionary<string, ProjectNode>
                {
                    {
                        _project1Path,
                        new ProjectNode
                        {
                            Path = _project1Path,
                            Dependencies = new HashSet<string> { _project2Path }
                        }
                    },
                    {
                        _project2Path,
                        new ProjectNode
                        {
                            Path = _project2Path,
                            Dependencies = new HashSet<string> { _project3Path }
                        }
                    },
                    {
                        _project3Path,
                        new ProjectNode
                        {
                            Path = _project3Path,
                            Dependencies = new HashSet<string>()
                        }
                    }
                }
            };

            // Calculate and set checksum
            cache.Checksum = DependencyGraphCache.CalculateChecksum(_solutionPath, projectFiles);

            // Act
            cache.Save();
            var loadedCache = DependencyGraphCache.Load(_solutionPath, projectFiles);

            // Assert
            Assert.NotNull(loadedCache);
            Assert.Equal(_solutionPath, loadedCache.SolutionPath);
            Assert.Equal(3, loadedCache.Projects.Count);
            Assert.Contains(_project2Path, loadedCache.Projects[_project1Path].Dependencies);
            Assert.Contains(_project3Path, loadedCache.Projects[_project2Path].Dependencies);
            Assert.Empty(loadedCache.Projects[_project3Path].Dependencies);
        }

        [Fact]
        public void Cache_ShouldTrackTransitiveDependencies()
        {
            // Arrange
            var cache = new DependencyGraphCache
            {
                SolutionPath = _solutionPath,
                Projects = new Dictionary<string, ProjectNode>
                {
                    {
                        _project1Path,
                        new ProjectNode
                        {
                            Path = _project1Path,
                            Dependencies = new HashSet<string> { _project2Path }
                        }
                    },
                    {
                        _project2Path,
                        new ProjectNode
                        {
                            Path = _project2Path,
                            Dependencies = new HashSet<string> { _project3Path }
                        }
                    },
                    {
                        _project3Path,
                        new ProjectNode
                        {
                            Path = _project3Path,
                            Dependencies = new HashSet<string>()
                        }
                    }
                }
            };

            // Act
            var allDependencies = cache.GetProjectDependencies(_project1Path);
            var dependentProjects = cache.GetDependentProjects(_project3Path);

            // Assert
            Assert.Equal(2, allDependencies.Count);
            Assert.Contains(_project2Path, allDependencies);
            Assert.Contains(_project3Path, allDependencies);

            Assert.Equal(2, dependentProjects.Count);
            Assert.Contains(_project1Path, dependentProjects);
            Assert.Contains(_project2Path, dependentProjects);
        }

        [Fact]
        public void Cache_ShouldBeInvalidatedWhenProjectChanges()
        {
            // Arrange
            var projectFiles = new[] { _project1Path, _project2Path };
            var cache = new DependencyGraphCache
            {
                SolutionPath = _solutionPath,
                Projects = new Dictionary<string, ProjectNode>
                {
                    {
                        _project1Path,
                        new ProjectNode
                        {
                            Path = _project1Path,
                            Dependencies = new HashSet<string> { _project2Path }
                        }
                    },
                    {
                        _project2Path,
                        new ProjectNode
                        {
                            Path = _project2Path,
                            Dependencies = new HashSet<string>()
                        }
                    }
                }
            };

            cache.Checksum = DependencyGraphCache.CalculateChecksum(_solutionPath, projectFiles);
            cache.Save();

            // Act - modify a project file
            File.WriteAllText(_project1Path, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
            var loadedCache = DependencyGraphCache.Load(_solutionPath, projectFiles);

            // Assert
            Assert.Null(loadedCache); // Cache should be invalid
        }

        [Fact]
        public void Cache_ShouldBeInvalidatedWhenSolutionChanges()
        {
            // Arrange
            var projectFiles = new[] { _project1Path, _project2Path };
            var cache = new DependencyGraphCache
            {
                SolutionPath = _solutionPath,
                Projects = new Dictionary<string, ProjectNode>
                {
                    {
                        _project1Path,
                        new ProjectNode
                        {
                            Path = _project1Path,
                            Dependencies = new HashSet<string> { _project2Path }
                        }
                    },
                    {
                        _project2Path,
                        new ProjectNode
                        {
                            Path = _project2Path,
                            Dependencies = new HashSet<string>()
                        }
                    }
                }
            };

            cache.Checksum = DependencyGraphCache.CalculateChecksum(_solutionPath, projectFiles);
            cache.Save();

            // Act - modify the solution file
            File.WriteAllText(_solutionPath, "modified solution content");
            var loadedCache = DependencyGraphCache.Load(_solutionPath, projectFiles);

            // Assert
            Assert.Null(loadedCache); // Cache should be invalid
        }

        [Fact]
        public void Cache_ShouldHandleMissingFiles()
        {
            // Arrange
            var nonExistentSolution = Path.Combine(_tempDir, "nonexistent.sln");
            var projectFiles = new[] { _project1Path, _project2Path };

            // Act
            var loadedCache = DependencyGraphCache.Load(nonExistentSolution, projectFiles);

            // Assert
            Assert.Null(loadedCache);
        }

        [Fact]
        public void Cache_ShouldConvertBetweenFormats()
        {
            // Arrange
            var incrementalistGraph = new Dictionary<string, ICollection<string>>
            {
                // In Incrementalist's format, the values are projects that depend on the key
                { _project3Path, new HashSet<string> { _project1Path, _project2Path } },  // Project3 is depended on by Project1 and Project2
                { _project2Path, new HashSet<string> { _project1Path } }                  // Project2 is depended on by Project1
            };

            // Act
            var cache = DependencyGraphCache.FromDependencyGraph(_solutionPath, incrementalistGraph);
            var convertedBack = cache.ToIncrementalistDependencyGraph();

            // Assert
            // Verify the cache structure (our tree format)
            Assert.Equal(3, cache.Projects.Count);
            
            // Project1 depends on Project2 and Project3
            Assert.Contains(_project2Path, cache.Projects[_project1Path].Dependencies);
            Assert.Contains(_project3Path, cache.Projects[_project1Path].Dependencies);
            
            // Project2 depends on Project3
            Assert.Contains(_project3Path, cache.Projects[_project2Path].Dependencies);
            
            // Project3 has no dependencies
            Assert.Empty(cache.Projects[_project3Path].Dependencies);

            // Verify the conversion back to Incrementalist's format
            Assert.Equal(incrementalistGraph.Count, convertedBack.Count);
            Assert.Equal(incrementalistGraph[_project3Path].Count, convertedBack[_project3Path].Count);
            Assert.Equal(incrementalistGraph[_project2Path].Count, convertedBack[_project2Path].Count);
            
            // Verify the specific dependencies
            Assert.Contains(_project1Path, convertedBack[_project3Path]);
            Assert.Contains(_project2Path, convertedBack[_project3Path]);
            Assert.Contains(_project1Path, convertedBack[_project2Path]);
        }
    }
} 