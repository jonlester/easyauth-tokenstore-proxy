using EasyAuthTokenstoreProxy.Configuration;

namespace EasyAuthTokenstoreProxy.Configuration;

public class BlobStorageConfiguration
{
    public BlobContainerConfig PrimaryContainer { get; set; } = new();
    public BlobContainerConfig SecondaryReadContainer { get; set; } = new();
    
    public static BlobStorageConfiguration FromEnvironment()
    {
        var primaryUrl = Environment.GetEnvironmentVariable("BLOB_PRIMARY_CONTAINER_URL");
        var secondaryUrl = Environment.GetEnvironmentVariable("BLOB_SECONDARY_READ_CONTAINER_URL");
        
        if (string.IsNullOrWhiteSpace(primaryUrl))
        {
            throw new InvalidOperationException("BLOB_PRIMARY_CONTAINER_URL environment variable is required");
        }
        
        if (string.IsNullOrWhiteSpace(secondaryUrl))
        {
            throw new InvalidOperationException("BLOB_SECONDARY_READ_CONTAINER_URL environment variable is required");
        }
        
        return new BlobStorageConfiguration
        {
            PrimaryContainer = BlobContainerConfig.Parse(primaryUrl),
            SecondaryReadContainer = BlobContainerConfig.Parse(secondaryUrl)
        };
    }
}