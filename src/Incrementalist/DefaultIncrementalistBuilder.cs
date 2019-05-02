// -----------------------------------------------------------------------
// <copyright file="DefaultIncrementalistBuilder.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

namespace Incrementalist
{
    ///// <summary>
    ///// Default implementation of builder interface.
    ///// </summary>
    ///// <typeparam name="TOutput">The type of output expected.</typeparam>
    //public sealed class DefaultIncrementalistBuilder<TOutput> : IIncrementalistBuilder<TOutput>
    //{
    //    public BuildSettings Settings { get; private set; }
    //    public ILogger Logger { get; private set; }

    //    private readonly List<IBuildCommand> _cmds = new List<IBuildCommand>();

    //    public IEnumerable<string> ExecutionPlan => _cmds.Select(x => x.Name);

    //    public IIncrementalistBuilder<TOutput> WithSettings(BuildSettings settings)
    //    {
    //        Settings = settings;
    //        return this;
    //    }

    //    public IIncrementalistBuilder<TOutput> WithLogger(ILogger logger)
    //    {
    //        Logger = logger;
    //        return this;
    //    }

    //    public IIncrementalistBuilder<TOutput> Next(IBuildCommand cmd)
    //    {
    //        _cmds.Add(cmd);
    //        return this;
    //    }

    //    public void Validate()
    //    {
    //        Contract.Assert(Settings != null, "Settings must not be null.");
    //        Contract.Assert(Logger != null, "Logger must not be null");
    //        Contract.Assert(_cmds.Count > 0, "Must have at least one command");
    //    }

    //    public async Task<TOutput> Run()
    //    {
    //        var previous = Task.FromResult(new object());
    //        foreach (var task in _cmds)
    //        {
    //            previous = task.Process(previous);
    //        }

    //        return (TOutput)await previous;
    //    }
    //}
}