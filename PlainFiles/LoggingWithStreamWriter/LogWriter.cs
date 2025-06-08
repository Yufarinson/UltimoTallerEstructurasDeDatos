namespace LoggingWithStreamWriter;

public class LogWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private bool _disposed = false;

    public LogWriter(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(path, append: true)
        {
            AutoFlush = true
        };
    }

    public void WriteLog(string level, string message)
    {
        if (_disposed) return;
        var tiemstamp = DateTime.Now.ToString("s");
        _writer.WriteLine($"{tiemstamp} [{level.ToUpper()}] {message}");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _writer?.Dispose();
            }
            _disposed = true;
        }
    }

    ~LogWriter()
    {
        Dispose(false);
    }
}