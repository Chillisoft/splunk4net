﻿using System;
using System.Net;
using log4net;
using log4net.Config;
using log4net.Repository.Hierarchy;

namespace TestAppUsingNugetPackage
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true;

            XmlConfigurator.Configure();
            ManuallyConfigureSplunkAppender();

            var log = LogManager.GetLogger(typeof (Program));

            Console.WriteLine("Write lines to log or Ctrl-C to quit");
            while(true)
            {
                var firstPart = Console.ReadLine();
                var data = new Data() { FirstPart = firstPart, SecondPart = "bar", Child = new Data() { FirstPart = "first", SecondPart = "second" }}; 
                log.Info(data);
                log.Warn("As a string: " + firstPart);
            }
            // ReSharper disable once FunctionNeverReturns // on purpose...
        }

        private static void ManuallyConfigureSplunkAppender()
        {
            // you probably don't want to put sensitive data in some app configs (eg,
            //  on a client app), so you can configure with logic similar to this:
            var heirachy = (Hierarchy) LogManager.GetRepository();
            foreach (var appender in heirachy.Root.Appenders)
            {
                if (appender.Name == "SplunkAppender")
                {
                    appender.SetProperty<string>("Login", "admin");
                    appender.SetProperty<string>("Password", "P4$$w0rd");
                }
            }
        }
    }
}
