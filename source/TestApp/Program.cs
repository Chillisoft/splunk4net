using System;
using System.Net;
using System.Security.AccessControl;
using log4net;
using log4net.Config;
using log4net.Repository.Hierarchy;
using Newtonsoft.Json;

namespace TestApp
{
    public class Data
    {
        public string FirstPart { get; set; }
        public string SecondPart { get; set; }
        public Data Child { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
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

    public static class ObjectExtensions
    {
        public static void SetProperty<T>(this object obj, string propertyName, object value)
        {
            var propInfo = obj.GetType().GetProperty(propertyName);
            if (propInfo == null)
                return;
            if (!typeof (T).IsAssignableFrom(propInfo.PropertyType))
                return;
            propInfo.SetValue(obj, value);
        }
    }
}
