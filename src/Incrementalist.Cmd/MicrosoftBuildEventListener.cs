using System;
using System.Diagnostics.Tracing;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Incrementalist.Cmd;

/// <summary>
/// An <see cref="EventListener"/> that forwards the <c>Microsoft-Build</c> events to the provided <see cref="ILogger"/>.
/// </summary>
public class MicrosoftBuildEventListener : EventListener
{
    private readonly ILogger _logger;
    private readonly EventLevel _level;

    /// <summary>
    /// Initialize a new instance of the <see cref="MicrosoftBuildEventListener"/> class.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> where the tracing events will be written.</param>
    /// <param name="level">The <see cref="EventLevel"/> to enable for the <c>Microsoft-Build</c> events.</param>
    public MicrosoftBuildEventListener(ILogger logger, EventLevel level)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _level = level;
    }

    /// <inheritdoc/>
    protected sealed override void OnEventSourceCreated(EventSource eventSource)
    {
        base.OnEventSourceCreated(eventSource);

        if (eventSource.Name == "Microsoft-Build")
        {
            EnableEvents(eventSource, _level);
        }
    }

    /// <inheritdoc/>
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        base.OnEventWritten(eventData);

        // What's the purpose of the _level parameter in the EnableEvents(eventSource, _level) method?
        if (eventData.Level > _level)
        {
            return;
        }

        var message = eventData.Message;
        var payloadNames = eventData.PayloadNames?.Select(e => "{" + Capitalized(e) + "}").ToArray() ?? Array.Empty<object>();
        var messageTemplate = message == null ? string.Join(" ", eventData.PayloadNames?.Select(e => $"{Capitalized(e)}: {{{Capitalized(e)}}}").Prepend(eventData.EventName) ?? []) : string.Format(message, payloadNames);
        var propertyValues = eventData.Payload?.ToArray() ?? [];
        var level = ConvertLevel(eventData.Level);
        _logger.Log(level, new EventId(eventData.EventId, eventData.EventName), messageTemplate, propertyValues);
    }

    private static LogLevel ConvertLevel(EventLevel eventLevel) =>
        eventLevel switch
        {
            EventLevel.LogAlways => LogLevel.Information, // should not happen if the EventSource is written properly, but fallback to Information in case it ever happens
            EventLevel.Critical => LogLevel.Critical,
            EventLevel.Error => LogLevel.Error,
            EventLevel.Warning => LogLevel.Warning,
            EventLevel.Informational => LogLevel.Information,
            EventLevel.Verbose => LogLevel.Trace,
            _ => throw new ArgumentOutOfRangeException(nameof(eventLevel), eventLevel, $"The value of argument '{nameof(eventLevel)}' ({eventLevel}) is invalid for enum type '{nameof(EventLevel)}'.")
        };

    private static string? Capitalized(string? input) =>
        input switch
        {
            null => null,
            "" => "",
            _ => $"{char.ToUpperInvariant(input[0])}{input.AsSpan(1)}"
        };
}