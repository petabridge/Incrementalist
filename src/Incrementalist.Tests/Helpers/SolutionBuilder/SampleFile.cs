using System.IO;

namespace Incrementalist.Tests.Helpers;

/// <summary>
/// Sample file data
/// </summary>
/// <param name="Name">Name of the file (might be used by another generated files)</param>
/// <param name="Content">File content</param>
public sealed record SampleFile(string Name, string Content) : IMsBuildSerializable
{
    /// <summary>
    /// Gets full file path
    /// </summary>
    public string GetFullPath(string basePath) => Path.Combine(basePath, Name);

    public string Serialize()
    {
        return Content;
    }
}

public static class CsharpSamples
{
    public const string HelloClass = """
                                     public class Hello
                                     {
                                         public string SayHello()
                                         {
                                             return "Hello, world!";
                                         }
                                     }
                                     """;
    
    public const string HelloClassWithNamespace = """
                                                 namespace HelloWorld
                                                 {
                                                     public class Hello
                                                     {
                                                         public string SayHello()
                                                         {
                                                             return "Hello, world!";
                                                         }
                                                     }
                                                 }
                                                 """;
    
    // do a Goodbye class with a method that returns "Goodbye, world!"
    public const string GoodbyeClass = """
                                        public class Goodbye
                                        {
                                            public string SayGoodbye()
                                            {
                                                return "Goodbye, world!";
                                            }
                                        }
                                        """;
    
    public const string GoodbyeClassWithNamespace = """
                                                    namespace GoodbyeWorld
                                                    {
                                                        public class Goodbye
                                                        {
                                                            public string SayGoodbye()
                                                            {
                                                                return "Goodbye, world!";
                                                            }
                                                        }
                                                    }
                                                    """;
    
    // do a Foo class
    public const string FooClass = """
                                    public class Foo
                                    {
                                        public string SayFoo()
                                        {
                                            return "Foo, world!";
                                        }
                                    }
                                    """;
    
    // do a Bar class
    public const string BarClass = """
                                    public class Bar
                                    {
                                        public string SayBar()
                                        {
                                            return "Bar, world!";
                                        }
                                    }
                                    """;
}