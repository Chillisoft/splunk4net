namespace splunk4net.Splunk
{
    public class SplunkWriterConfig
    {
        public string AppenderName { get; private set; }
        public string IndexName { get; private set; }
        public string RemoteUrl { get; private set; }
        public string Login { get; private set; }
        public string Password { get; private set; }
        public SplunkWriterConfig(string appenderName, 
                                    string indexName, 
                                    string remoteUrl, 
                                    string login, 
                                    string password)
        {
            AppenderName = appenderName;
            IndexName = indexName;
            RemoteUrl = remoteUrl;
            Login = login;
            Password = password;
        }
    }
}