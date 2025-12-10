namespace EasyAuthTokenstoreProxy.Helpers;

public static class HttpResponseHelper
{
    public static async Task CopyResponse(HttpResponseMessage response, HttpContext context, ILogger logger, string? blobSource = null)
    {
        // Copy response status and headers
        context.Response.StatusCode = (int)response.StatusCode;
        
        // Add blob source header if provided
        if (!string.IsNullOrEmpty(blobSource))
        {
            context.Response.Headers["x-easyauth-tokenstore-container"] = blobSource;
        }
        
        foreach (var header in response.Headers)
        {
            context.Response.Headers.TryAdd(header.Key, header.Value.ToArray());
        }
        
        if (response.Content != null)
        {
            foreach (var header in response.Content.Headers)
            {
                context.Response.Headers.TryAdd(header.Key, header.Value.ToArray());
            }
            
            // Stream response directly
            await response.Content.CopyToAsync(context.Response.Body);
        }
    }
}