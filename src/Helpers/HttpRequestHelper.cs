namespace EasyAuthTokenstoreProxy.Helpers;

public static class HttpRequestHelper
{
    public static HttpRequestMessage CreateRequest(HttpContext context, string targetUrl)
    {
        var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUrl);
        
        // Copy headers (except Host)
        foreach (var header in context.Request.Headers)
        {
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                continue;
                
            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
        
        return requestMessage;
    }
}