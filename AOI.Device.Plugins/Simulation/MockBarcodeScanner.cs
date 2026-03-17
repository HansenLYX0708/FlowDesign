using AOI.Device.Abstractions.Base;
using AOI.Device.Abstractions.Scanner;

namespace AOI.Device.Plugins.Simulation;

public class MockBarcodeScanner : DeviceBase, IBarcodeScanner
{
    public event Action<string>? BarcodeReceived;

    public async Task<string> ScanAsync()
    {
        await Task.Delay(200);

        var code = Guid.NewGuid().ToString()[..8];

        BarcodeReceived?.Invoke(code);

        return code;
    }
}