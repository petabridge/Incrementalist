using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Incrementalist
{
    /// <summary>
    /// Abstract base class with built-in logging support and templating
    /// </summary>
    public abstract class BuildCommandBase : IBuildCommand
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

        public async Task<object> Process(Task<object> previousTask)
        {
            Logger.LogDebug("[{0}] - Entered Task", Name);

            if (CancellationToken.IsCancellationRequested)
            {
                Logger.LogInformation("Cancellation requested. Terminating Incrementalist at stage [{0}]", Name);
                CancellationToken.ThrowIfCancellationRequested();
            }

            try
            {
                return await ProcessImpl(previousTask);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[{0}] - Catastrophic task failure.", Name);
                throw;
            }
            finally
            {
                Logger.LogDebug("[{0}] - Exited Task", Name);
            }
        }

        protected abstract Task<object> ProcessImpl(Task<object> previousTask);
    }
}