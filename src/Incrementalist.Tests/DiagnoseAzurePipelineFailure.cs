// -----------------------------------------------------------------------
// <copyright file="DiagnoseAzurePipelineFailure.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace Incrementalist.Tests;

public class DiagnoseAzurePipelineFailure(ITestOutputHelper output)
{
    [WindowsOnlyFact]
    public void DiagnoseTypeInitializationException()
    {
        // Here's how this test fails on the Azure pipeline
        // Incrementalist.Tests.DiagnoseAzurePipelineFailure.DiagnoseTypeInitializationException [FAIL]
        //   System.MissingMethodException : Method not found: 'Boolean Microsoft.Build.Framework.NativeMethods.get_IsMono()'.
        //   Stack Trace:
        //     D:\a\1\s\src\Incrementalist.Tests\DiagnoseAzurePipelineFailure.cs(26,0): at Incrementalist.Tests.DiagnoseAzurePipelineFailure.DiagnoseTypeInitializationException()
        //        at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
        //        at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
        //
        // The NativeMethods.IsMono property was removed by https://github.com/dotnet/msbuild/pull/9745
        // And indeed it doesn't exist as of Microsoft.Build 17.11.4
        // It was first removed in v17.10.0-preview-24127-03, see https://github.com/dotnet/msbuild/commit/3f6dd37bc7a4ee5d1a75e4d690b7278c5cd91683
        // QUESTION: How does calling ToolLocationHelper.GetPlatformSDKLocation("Windows", "7.0") ends up calling a method that does not exist?
        try
        {
            WriteAssemblyInformation(output, typeof(Microsoft.Build.Logging.BinaryLogger).Assembly);
            WriteAssemblyInformation(output, typeof(Microsoft.Build.Framework.ITask).Assembly);
            WriteAssemblyInformation(output, typeof(Microsoft.Build.Tasks.MSBuild).Assembly);
            WriteAssemblyInformation(output, typeof(Microsoft.Build.Utilities.ToolLocationHelper).Assembly);

            var location = Microsoft.Build.Utilities.ToolLocationHelper.GetPlatformSDKLocation("Windows", "7.0");
            output.WriteLine($"🛠️ Calling ToolLocationHelper.GetPlatformSDKLocation(Windows, 7.0) returned \"{location}\"");
        }
        catch (TypeInitializationException exception)
        {
            output.WriteLine($"💥 TypeInitializationException 💥 {exception}");
            if (exception.InnerException != null)
            {
                output.WriteLine($"💥 InnerException 💥 {exception.InnerException}");
                throw exception.InnerException;
            }

            throw;
        }
    }

    private static void WriteAssemblyInformation(ITestOutputHelper output, Assembly assembly)
    {

        output.WriteLine($"📍 {assembly}");
        output.WriteLine($"   Version:  {assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}");
        output.WriteLine($"   Location: {assembly.Location}");
    }

    private sealed class WindowsOnlyFactAttribute : FactAttribute
    {
        public WindowsOnlyFactAttribute()
        {
            if (!OperatingSystem.IsWindows())
            {
                Skip = "Windows only";
            }
        }
    }
}