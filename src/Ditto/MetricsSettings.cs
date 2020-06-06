namespace Ditto
{
    public class MetricsSettings
    {
        public bool Enabled { get; set; }
        public string Path { get; set; } = "metrics/";
        public int Port { get; set; } = 5000;
    }
}