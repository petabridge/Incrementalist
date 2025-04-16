// -----------------------------------------------------------------------
// <copyright file="BuildCommandBase.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
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
            Logger = new WrappedLogger(logger, name);
            CancellationToken = cancellationToken;
        }

        protected ILogger Logger { get; }

        protected CancellationToken CancellationToken { get; }

        public string Name { get; }

        public async Task<TOut> Process(Task<TIn> previousTask)
        {
            Logger.LogDebug("Entered Task");

            if (CancellationToken.IsCancellationRequested)
            {
                Logger.LogInformation("Cancellation requested. Terminating Incrementalist at stage [{StageName}]", Name);
                CancellationToken.ThrowIfCancellationRequested();
            }

            try
            {
                return await ProcessImpl(previousTask);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Catastrophic task failure.");
                throw;
            }
            finally
            {
                Logger.LogDebug("Exited Task");
            }
        }

        protected abstract Task<TOut> ProcessImpl(Task<TIn> previousTask);
    }
}