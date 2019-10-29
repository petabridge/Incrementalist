using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Incrementalist.Tests.Helpers
{
    public class TestOutputLogger : ILogger
    {
        private readonly ITestOutputHelper _outputHelper;

        public TestOutputLogger(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }
        
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
           _outputHelper.WriteLine(formatter(state, exception));
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable BeginScope<TState>(TState state) => null;
    }
}