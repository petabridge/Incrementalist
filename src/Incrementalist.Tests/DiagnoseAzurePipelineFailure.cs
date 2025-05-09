// -----------------------------------------------------------------------
// <copyright file="DiagnoseAzurePipelineFailure.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using Microsoft.Build.Utilities;
using Xunit;

namespace Incrementalist.Tests;

public class DiagnoseAzurePipelineFailure
{
    [WindowsOnlyFact]
    public void DiagnoseTypeInitializationException()
    {
        try
        {
            var location = ToolLocationHelper.GetPlatformSDKLocation("Windows", "7.0");
            throw new Exception($"Calling ToolLocationHelper.GetPlatformSDKLocation(Windows, 7.0) returned \"{location}\"");
        }
        catch (TypeInitializationException exception)
        {
            if (exception.InnerException != null)
                throw exception.InnerException;

            throw;
        }
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