// -----------------------------------------------------------------------
// <copyright file="BuildCommandBase.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Incrementalist
{
    /// <summary>
    ///     Abstract base class with built-in logging support and templating
    /// </summary>
    public abstract class BuildCommandBase<TIn, TOut> : IBuildCommand<TIn, TOut>
    {
        protected BuildCommandBase(string name, ILogger logger, CancellationToken cancellationToken)
        {
            Name = name;
            Logger = logger;
            CancellationToken = cancellationToken;
        }

        protected ILogger Logger { get; }

        protected CancellationToken CancellationToken { get; }

        public string Name { get; }

        public async Task<TOut> Process(Task<TIn> previousTask)
        {
            Logger.LogDebug("[{TaskName}] - Entered Task", Name);

            if (CancellationToken.IsCancellationRequested)
            {
                Logger.LogInformation("Cancellation requested. Terminating Incrementalist at stage [{TaskName}]", Name);
                CancellationToken.ThrowIfCancellationRequested();
            }

            try
            {
                return await ProcessImpl(previousTask);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[{TaskName}] - Catastrophic task failure.", Name);
                throw;
            }
            finally
            {
                Logger.LogDebug("[{TaskName}] - Exited Task", Name);
            }
        }

        protected abstract Task<TOut> ProcessImpl(Task<TIn> previousTask);
    }
}