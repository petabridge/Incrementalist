namespace Incrementalist.Tests.Helpers;

public static class SolutionFileSamples
{
    public const string GlobalJson = """
                                     {
                                         "sdk": {
                                             "version": "9.0.100"
                                         }
                                     }
                                     """;
    
    public const string DirectoryBuildProps = """
                                              <Project>
                                                  <PropertyGroup>
                                                      <OutputPath>bin\</OutputPath>
                                                  </PropertyGroup>
                                              </Project>
                                              """;
    
    public const string DirectoryPackagesProps = """
                                                 <Project>
                                                     <ItemGroup>
                                                         <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
                                                     </ItemGroup>
                                                 </Project>
                                                 """;

    public const string NuGetConfig = """
                                      <?xml version="1.0" encoding="utf-8"?>
                                      <configuration>
                                        <solution>
                                          <add key="disableSourceControlIntegration" value="true" />
                                        </solution>
                                        <packageSources>
                                          <clear />
                                          <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                                        </packageSources>
                                      </configuration>
                                      """;
}