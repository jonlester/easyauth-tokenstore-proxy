# Azure App Service Sidecar Deployment Guide

This container is designed to be deployed as a **sidecar container** alongside an Azure App Service web app, not as a standalone web application.

## Sidecar Container Pattern

In the sidecar pattern:
- **Main App**: Your primary Azure App Service web application (handles HTTPS, authentication, routing)
- **Sidecar Container**: This blob storage proxy (internal HTTP communication only)

## Key Differences from Standard Web App Deployment

### 1. **No Direct HTTPS Handling**
- The sidecar container uses HTTP internally (port 8080)
- HTTPS is handled by the main App Service application
- All external traffic goes through the main app first

### 2. **Internal Communication Only**
- The sidecar is not directly accessible from the internet
- Communication flows: `Client → Main App (HTTPS) → Sidecar (HTTP)`

### 3. **Simplified Configuration**
- No SSL certificates required in the sidecar
- No HTTPS redirection or HSTS headers
- Streamlined container focused on blob proxy functionality

## Deployment Steps

### 1. **Prepare Your Main App**

Your main Azure App Service application needs to route blob storage requests to the sidecar:

```csharp
// In your main app's controller or middleware
[HttpGet("api/blobs/{**blobPath}")]
public async Task<IActionResult> ProxyToSidecar(string blobPath)
{
    using var httpClient = new HttpClient();
    var sidecarUrl = $"http://localhost:8080/blobs/{blobPath}";
    var response = await httpClient.GetAsync(sidecarUrl);
    
    return new FileStreamResult(await response.Content.ReadAsStreamAsync(), 
                               response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream");
}
```

### 2. **Configure App Service for Sidecar**

Set these environment variables in your main App Service:

```bash
# Blob Storage Configuration (for the sidecar)
BLOB_PRIMARY_CONTAINER_URL=https://yourstorageaccount.blob.core.windows.net/primary-container?sv=2022-11-02&ss=b&srt=co&sp=rwdlacx&se=2025-12-31T23:59:59Z&st=2025-01-01T00:00:00Z&spr=https&sig=YourSasSignature
BLOB_SECONDARY_READ_CONTAINER_URL=https://yourstorageaccount.blob.core.windows.net/secondary-container?sv=2022-11-02&ss=b&srt=co&sp=rl&se=2025-12-31T23:59:59Z&st=2025-01-01T00:00:00Z&spr=https&sig=YourSasSignature
```

### 3. **Deploy Using Azure CLI**

```bash
# Enable sidecar containers (preview feature)
az extension add --name containerapp

# Configure your main app with sidecar
az webapp config container set \
  --resource-group myResourceGroup \
  --name myMainApp \
  --sidecar-containers '[
    {
      "name": "blob-proxy",
      "image": "your-registry.azurecr.io/easyauth-tokenstore-proxy:latest",
      "resources": {
        "cpu": 0.5,
        "memory": "1Gi"
      },
      "environmentVariables": [
        {"name": "BLOB_PRIMARY_CONTAINER_URL", "secureValue": "https://yourstorageaccount.blob.core.windows.net/primary-container?sv=2022-11-02&ss=b&srt=co&sp=rwdlacx&se=2025-12-31T23:59:59Z&st=2025-01-01T00:00:00Z&spr=https&sig=YourPrimarySasSignature"},
        {"name": "BLOB_SECONDARY_READ_CONTAINER_URL", "secureValue": "https://yourstorageaccount.blob.core.windows.net/secondary-container?sv=2022-11-02&ss=b&srt=co&sp=rl&se=2025-12-31T23:59:59Z&st=2025-01-01T00:00:00Z&spr=https&sig=YourSecondarySasSignature"}
      ]
    }
  ]'
```

### 4. **Container Registry Setup**

```bash
# Build and push to Azure Container Registry (from project root)
az acr build --registry myregistry --image easyauth-tokenstore-proxy:latest --file src/Dockerfile .

# Or using Docker (from project root)
docker build -f src/Dockerfile -t myregistry.azurecr.io/easyauth-tokenstore-proxy:latest .
docker push myregistry.azurecr.io/easyauth-tokenstore-proxy:latest
```

## Communication Flow

```
Internet (HTTPS) → Azure App Service (Main App) 
                     ↓ (HTTP localhost)
                   Sidecar Container (Blob Proxy)
                     ↓ (HTTPS)
                   Azure Blob Storage
```

## Benefits of Sidecar Pattern

1. **Security**: No direct internet exposure of the proxy
2. **Scalability**: Independent scaling of main app and sidecar
3. **Isolation**: Blob operations don't affect main app performance
4. **Simplicity**: No certificate management in sidecar
5. **Cost Effective**: Shared compute resources

## Monitoring & Health Checks

The sidecar includes health endpoints for monitoring:

```bash
# Health check from main app
GET http://localhost:8080/health

# Blob storage connectivity check
GET http://localhost:8080/health/storage
```

Your main app can periodically check sidecar health and implement fallback logic if needed.

## Troubleshooting

### Common Issues

1. **Sidecar not accessible**: Check that both containers are in the same App Service plan
2. **Storage access denied**: Verify SAS token has correct permissions and hasn't expired
3. **Memory issues**: Monitor resource usage and adjust sidecar resource limits

### Logs

Access sidecar logs through Azure portal:
```bash
# Stream logs
az webapp log tail --name myMainApp --resource-group myResourceGroup --provider kudu
```

## Security Considerations

- **Network Isolation**: Sidecar communicates only with main app and Azure Storage
- **Authentication**: Main app handles all authentication/authorization
- **Secrets**: Use Key Vault references for sensitive configuration
- **Least Privilege**: SAS token should have minimal required permissions

## Example Main App Integration

```csharp
public class BlobProxyService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public BlobProxyService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task<Stream> GetBlobAsync(string blobPath)
    {
        var sidecarUrl = $"http://localhost:8080/blobs/{blobPath}";
        var response = await _httpClient.GetAsync(sidecarUrl);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync();
    }

    public async Task<bool> UploadBlobAsync(string blobPath, Stream content)
    {
        var sidecarUrl = $"http://localhost:8080/blobs/{blobPath}";
        using var streamContent = new StreamContent(content);
        var response = await _httpClient.PutAsync(sidecarUrl, streamContent);
        return response.IsSuccessStatusCode;
    }
}
```

This sidecar deployment provides a secure, scalable solution for blob storage operations while maintaining the simplicity and security benefits of the App Service platform.