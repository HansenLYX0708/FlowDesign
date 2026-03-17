using AOI.Device.Abstractions.Base;

namespace AOI.Device.Abstractions.Scanner;

public interface IBarcodeScanner : IDevice
{
    Task<string> ScanAsync();

    event Action<string>? BarcodeReceived;
}