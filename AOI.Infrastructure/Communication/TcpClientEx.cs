using System.Net.Sockets;
using System.Text;

namespace AOI.Infrastructure.Communication;

public class TcpClientEx
{
    private TcpClient? _client;

    public async Task ConnectAsync(string host, int port)
    {
        _client = new TcpClient();

        await _client.ConnectAsync(host, port);
    }

    public async Task SendAsync(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);

        await _client!.GetStream().WriteAsync(bytes);
    }

    public async Task<string> ReceiveAsync()
    {
        var buffer = new byte[1024];

        var count = await _client!.GetStream().ReadAsync(buffer);

        return Encoding.UTF8.GetString(buffer, 0, count);
    }
}