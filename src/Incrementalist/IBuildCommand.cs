using System.Collections.Generic;
using System.Threading.Tasks;

namespace Incrementalist
{
    /// <summary>
    /// An Incrementalist build instruction.
    /// </summary>
    public interface IBuildCommand<TIn, TOut>
    {
        string Name { get; }

        Task<TOut> Process(Task<TIn> previousTask);
    }
}