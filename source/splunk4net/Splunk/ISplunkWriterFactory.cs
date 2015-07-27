namespace splunk4net.Splunk
{
    public interface ISplunkWriterFactory
    {
        ISplunkWriter CreateFor(string appenderName);

        void RegisterConfigFor(string appenderName, 
            string indexName, 
            string remoteUrl, 
            string login, 
            string password);
    }
}