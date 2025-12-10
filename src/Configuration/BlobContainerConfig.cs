namespace EasyAuthTokenstoreProxy.Configuration;

public class BlobContainerConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    
    public string FullUrl => string.IsNullOrEmpty(Query) ? BaseUrl : $"{BaseUrl}?{Query}";
    
    public string MakeBlobUrl(string blobPath, string? incomingQueryString = null)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
        {
            throw new ArgumentException("Blob path cannot be null or empty", nameof(blobPath));
        }
        
        // Remove leading slash if present
        var cleanPath = blobPath.TrimStart('/');
        
        // Construct the full blob URL
        var blobUrl = $"{BaseUrl}/{cleanPath}";
        
        // Merge query parameters
        var mergedQuery = MergeQueryParameters(incomingQueryString, Query);
        
        return string.IsNullOrEmpty(mergedQuery) ? blobUrl : $"{blobUrl}?{mergedQuery}";
    }
    
    private static string MergeQueryParameters(string? incomingQuery, string sasQuery)
    {
        if (string.IsNullOrEmpty(incomingQuery) && string.IsNullOrEmpty(sasQuery))
            return string.Empty;
            
        if (string.IsNullOrEmpty(incomingQuery))
            return sasQuery;
            
        if (string.IsNullOrEmpty(sasQuery))
            return incomingQuery;
        
        // Parse both query strings - ParseQueryString automatically handles URL decoding
        var incomingParams = System.Web.HttpUtility.ParseQueryString(incomingQuery);
        var sasParams = System.Web.HttpUtility.ParseQueryString(sasQuery);
        
        // Create a new collection to avoid modifying the original
        var merged = System.Web.HttpUtility.ParseQueryString(string.Empty);
        
        // Add incoming parameters first
        foreach (string? key in incomingParams.AllKeys)
        {
            if (key != null)
            {
                merged[key] = incomingParams[key];
            }
        }
        
        // Add SAS parameters, overriding any conflicts
        foreach (string? key in sasParams.AllKeys)
        {
            if (key != null)
            {
                merged[key] = sasParams[key];
            }
        }
        
        // ToString() automatically URL-encodes the parameters properly
        return merged.ToString() ?? string.Empty;
    }
    
    public static BlobContainerConfig Parse(string containerUrl)
    {
        if (string.IsNullOrWhiteSpace(containerUrl))
        {
            throw new ArgumentException("Container URL cannot be null or empty", nameof(containerUrl));
        }

        var uri = new Uri(containerUrl);
        
        return new BlobContainerConfig
        {
            BaseUrl = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}",
            Query = uri.Query.TrimStart('?')
        };
    }
}