// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Microsoft.EntityFrameworkCore.Extensions.Internal
{
    internal static class CoreLoggerExtensions
    {
        public static void LogInformation<TState>(
            this ILogger logger, InMemoryLoggingEventId eventId, TState state, Func<TState, string> formatter)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.Log(LogLevel.Information, (int)eventId, state, null, (s, _) => formatter((TState)s));
            }
        }
    }
}
