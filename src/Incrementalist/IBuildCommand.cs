// -----------------------------------------------------------------------
// <copyright file="IBuildCommand.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

namespace Incrementalist
{
    /// <summary>
    ///     An Incrementalist build instruction.
    /// </summary>
    public interface IBuildCommand<TIn, TOut>
    {
        string Name { get; }

        Task<TOut> Process(Task<TIn> previousTask);
    }
}