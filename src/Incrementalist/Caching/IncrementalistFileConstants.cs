// -----------------------------------------------------------------------
// <copyright file="IncrementalistFileConstants.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

namespace Incrementalist.Caching
{
    /// <summary>
    /// Constants used for Incrementalist file system operations
    /// </summary>
    public static class IncrementalistFileConstants
    {
        /// <summary>
        /// The directory where Incrementalist stores its cache and other files
        /// </summary>
        public const string IncrementalistDirectory = ".incrementalist";

        /// <summary>
        /// The filename for the dependency graph cache
        /// </summary>
        public const string CacheFileName = "incrementalist.graphcache.json";

        public const string CurrentVersion = "1.0";
    }
} 