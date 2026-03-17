using AOI.Flow.Node;

namespace AOI.Flow.Nodes.Vision;

public class VisionProcessNode
{
    public async Task Process(object image)
    {
        await Task.Delay(200);

        Console.WriteLine("Vision Done");
    }
}