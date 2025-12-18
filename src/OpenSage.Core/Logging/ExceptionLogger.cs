using System;
using NLog;

namespace OpenSage.Core.Logging
{
    public static class ExceptionLogger
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static void LogError(Action action, string message)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, message);
                throw;
            }
        }

        public static T LogError<T>(Func<T> func, string message)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, message);
                throw;
            }
        }
    }
}
