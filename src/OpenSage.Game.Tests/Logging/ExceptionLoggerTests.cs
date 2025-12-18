using System;
using NLog;
using NLog.Config;
using NLog.Targets;
using OpenSage.Core.Logging;
using Xunit;

namespace OpenSage.Tests.Logging
{
    public class ExceptionLoggerTests
    {
        [Fact]
        public void LogError_Action_LogsExceptionAndRethrows()
        {
            var target = new MemoryTarget("mem") { Layout = "${longdate}|${level}|${logger}|${message}|${exception:format=toString}" };
            var config = new LoggingConfiguration();
            config.AddRuleForAllLevels(target);
            LogManager.Configuration = config;

            var ex = Assert.Throws<InvalidOperationException>(() => ExceptionLogger.LogError(() => throw new InvalidOperationException("boom"), "Test action failure"));
            Assert.Equal("boom", ex.Message);

            LogManager.Flush();
            Assert.NotEmpty(target.Logs);
            Assert.Contains("Test action failure", target.Logs[0]);
            Assert.Contains("InvalidOperationException", target.Logs[0]);
        }

        [Fact]
        public void LogError_Func_LogsExceptionAndRethrows()
        {
            var target = new MemoryTarget("mem2") { Layout = "${longdate}|${level}|${logger}|${message}|${exception:format=toString}" };
            var config = new LoggingConfiguration();
            config.AddRuleForAllLevels(target);
            LogManager.Configuration = config;

            var ex = Assert.Throws<InvalidOperationException>(() => ExceptionLogger.LogError<int>(() => throw new InvalidOperationException("boom2"), "Test func failure"));
            Assert.Equal("boom2", ex.Message);

            LogManager.Flush();
            Assert.NotEmpty(target.Logs);
            Assert.Contains("Test func failure", target.Logs[0]);
            Assert.Contains("InvalidOperationException", target.Logs[0]);
        }
    }
}
