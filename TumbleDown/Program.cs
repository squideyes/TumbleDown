// Copyright 2018 Louis S.Berman.
//
// This file is part of TumbleDown.
//
// TumbleDown is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published 
// by the Free Software Foundation, either version 3 of the License, 
// or (at your option) any later version.
//
// TumbleDown is distributed in the hope that it will be useful, but 
// WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the GNU 
// General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with TumbleDown.  If not, see <http://www.gnu.org/licenses/>.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using System;

namespace TumbleDown
{
    class Program
    {
        static void Main(string[] args)
        {
            var servicesProvider = GetServiceProvider();

            var worker = servicesProvider.GetRequiredService<Worker>();

            worker.Run(args);

            LogManager.Shutdown();
        }

        private static IServiceProvider GetServiceProvider()
        {
            var services = new ServiceCollection();

            services.AddTransient<Worker>();

            services.AddSingleton<ILoggerFactory, LoggerFactory>();

            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            services.AddLogging((builder) =>
                builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace));

            var serviceProvider = services.BuildServiceProvider();

            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            loggerFactory.AddNLog(new NLogProviderOptions
            {
                CaptureMessageTemplates = true,
                CaptureMessageProperties = true
            });

            LogManager.LoadConfiguration("NLog.config");

            return serviceProvider;
        }
    }
}
