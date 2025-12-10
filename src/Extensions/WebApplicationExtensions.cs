using EasyAuthTokenstoreProxy.Configuration;
using EasyAuthTokenstoreProxy.Helpers;
using EasyAuthTokenstoreProxy.Models;
using System.Diagnostics.CodeAnalysis;

namespace EasyAuthTokenstoreProxy.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication MapBlobProxyEndpoints(this WebApplication app)
    {
        app.MapHealthEndpoint();
        app.MapWriteOperations();
        app.MapReadOperations();
        
        return app;
    }
    
    private static void MapHealthEndpoint(this WebApplication app)
    {
        app.MapGet("/health", [UnconditionalSuppressMessage("Trimming", "IL2026")] (BlobStorageConfiguration config) => 
            Results.Ok(new HealthResponse { Status = "healthy", Timestamp = DateTime.UtcNow }));
    }
    
    private static void MapWriteOperations(this WebApplication app)
    {
        app.MapMethods("/{**blobPath}", new[] { "PUT", "POST", "DELETE" }, async (
            string blobPath,
            HttpContext context,
            BlobStorageConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<Program> logger) =>
        {
            try
            {
                var httpClient = httpClientFactory.CreateClient("BlobProxy");
                var targetUrl = config.PrimaryContainer.MakeBlobUrl(blobPath, context.Request.QueryString.Value?.TrimStart('?'));
                
                using var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUrl);
                
                // Copy headers (except Host)
                foreach (var header in context.Request.Headers)
                {
                    if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                        continue;
                        
                    requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
                
                // Copy content for PUT/POST with streaming
                if (context.Request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase) || 
                    context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                {
                    if (context.Request.ContentLength > 0 || context.Request.Headers.TransferEncoding.Any())
                    {
                        var content = new StreamContent(context.Request.Body);
                        
                        // Copy content headers
                        if (context.Request.ContentLength.HasValue)
                            content.Headers.ContentLength = context.Request.ContentLength.Value;
                            
                        if (!string.IsNullOrEmpty(context.Request.ContentType))
                            content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
                        
                        // Copy other content headers
                        foreach (var header in context.Request.Headers.Where(h => h.Key.StartsWith("Content-")))
                        {
                            if (!header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) &&
                                !header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                            {
                                content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                            }
                        }
                        
                        requestMessage.Content = content;
                    }
                }
                
                // Use HttpCompletionOption.ResponseHeadersRead for streaming response
                var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
                
                // Copy response status and headers
                context.Response.StatusCode = (int)response.StatusCode;
                
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
                    
                    // Stream response directly without buffering
                    await response.Content.CopyToAsync(context.Response.Body);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error proxying {Method} request to {BlobPath}", context.Request.Method, blobPath);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Internal server error");
            }
        });
    }
    
    private static void MapReadOperations(this WebApplication app)
    {
        app.MapMethods("/{**blobPath}", new[] { "GET", "HEAD" }, async (
            string blobPath,
            HttpContext context,
            BlobStorageConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<Program> logger) =>
        {
            var query = context.Request.QueryString.Value?.TrimStart('?');
            var primaryUrl = config.PrimaryContainer.MakeBlobUrl(blobPath, query);
            var secondaryUrl = config.SecondaryReadContainer.MakeBlobUrl(blobPath, query);
            
            using var cts = new CancellationTokenSource();
            
            try
            {
                var httpClient = httpClientFactory.CreateClient("BlobProxy");
                
                // Start primary request first
                var primaryRequest = HttpRequestHelper.CreateRequest(context, primaryUrl);
                var primaryTask = httpClient.SendAsync(primaryRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                
                // Prepare and send eager secondary request while waiting for primary
                var secondaryRequest = HttpRequestHelper.CreateRequest(context, secondaryUrl);
                var secondaryTask = httpClient.SendAsync(secondaryRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                
                // Wait for primary response headers
                var primaryResponse = await primaryTask;
                
                if (primaryResponse.IsSuccessStatusCode)
                {
                    // Primary succeeded - cancel secondary request and return primary response
                    cts.Cancel();
                    await HttpResponseHelper.CopyResponse(primaryResponse, context, logger, "primary");
                    return;
                }
                
                logger.LogWarning("Primary request failed with status {StatusCode}, falling back to secondary", 
                    primaryResponse.StatusCode);
                
                // Primary failed - get secondary response
                var secondaryResponse = await secondaryTask;
                await HttpResponseHelper.CopyResponse(secondaryResponse, context, logger, "secondary");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during read operation for {BlobPath}", blobPath);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Internal server error");
            }
        });
    }
}