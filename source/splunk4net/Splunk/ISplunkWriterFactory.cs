namespace splunk4net.Splunk
{
    public interface ISplunkWriterFactory
    {
        ISplunkWriter CreateFor(string appenderName);
    }
}