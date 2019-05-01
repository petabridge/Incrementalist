using System.Collections.Generic;
using System.Threading.Tasks;

namespace Incrementalist
{
    /// <summary>
    /// An Incrementalist build instruction.
    /// </summary>
    public interface IBuildCommand
    {
        string Name { get; }

        Task<object> Process(Task<object> previousTask);
    }
}