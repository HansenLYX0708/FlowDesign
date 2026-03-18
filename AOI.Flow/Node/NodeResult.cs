namespace AOI.Flow.Node;

public class NodeResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public static NodeResult Ok()
        => new() { Success = true };

    public static NodeResult Fail(string msg)
        => new() { Success = false, ErrorMessage = msg };
}