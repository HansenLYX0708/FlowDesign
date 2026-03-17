namespace AOI.Core.Utils;

public static class RetryHelper
{
    public static async Task RetryAsync(
        Func<Task> action,
        int retryCount = 3,
        int delayMs = 200)
    {
        for (int i = 0; i < retryCount; i++)
        {
            try
            {
                await action();
                return;
            }
            catch
            {
                if (i == retryCount - 1)
                    throw;

                await Task.Delay(delayMs);
            }
        }
    }
}