using System.Net;
using System.Net.Sockets;

namespace AOI.Infrastructure.Communication;

public class TcpServerEx
{
    private TcpListener? _listener;

    public async Task StartAsync(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);

        _listener.Start();

        while (true)
        {
            var client = await _listener.AcceptTcpClientAsync();

            _ = HandleClient(client);
        }
    }

    private async Task HandleClient(TcpClient client)
    {
        using var stream = client.GetStream();

        var buffer = new byte[1024];

        while (true)
        {
            var count = await stream.ReadAsync(buffer);

            if (count == 0)
                break;

            await stream.WriteAsync(buffer.AsMemory(0, count));
        }
    }
}