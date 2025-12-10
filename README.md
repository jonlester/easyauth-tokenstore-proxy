# EasyAuth Tokenstore Proxy

A simple proxy for Azure Blob Storage, designed to work as a sidecar container for EasyAuth token storage operations.

## Features

- **Streaming proxy** - forwarding of blob operations without buffering
- **Primary/Secondary failover** - READ operations from primary and secondary (backup) storage

## Local Development

### Prerequisites

- .NET 10 SDK
- Two Azure Blob Storage containers with SAS URLs
  - primary container requires - full permissions (`READ`, `ADD`, `CREATE`, `WRITE`, `DELETE`)
  - secondary container only requires `READ`

### Setup

1. **Clone and navigate to the project:**
   ```bash
   cd EasyAuthTokenstoreProxy
   ```

2. **Create your environment configuration:**
   ```bash
   copy .env .env.local
   ```

3. **Edit `.env.local` with your blob storage URLs:**
   ```
   BLOB_PRIMARY_CONTAINER_URL=https://yourstorageaccount.blob.core.windows.net/container?sp=racwdl&st=...
   BLOB_SECONDARY_READ_CONTAINER_URL=https://yourstorageaccount.blob.core.windows.net/container?sp=r&st=...
   ```

### Running Locally

```bash
dotnet run
```

The API will be available at `http://localhost:8080`

### Testing

Use the REST Client extension in VS Code with the provided `.http` file:

```
dev/EasyAuthTokenstoreProxy.http
```

**Basic operations:**
- `PUT /{blobPath}` - Upload blob (uses primary storage)
- `GET /{blobPath}` - Download blob (tries primary, fails over to secondary)
- `DELETE /{blobPath}` - Delete blob (uses primary storage)
- `GET /health` - Health check endpoint

### Response Headers

READ operations include an `x-easyauth-tokenstore-container` header in the response indicating which storage served the response:
- `primary` - Response came from primary storage
- `secondary` - Response came from secondary storage (primary failed)


## Container Deployment

The project includes an optimized `Dockerfile` for Azure App Service sidecar deployment on port 8080.

### Build Optimizations

This project is configured with .NET build optimizations for production deployment:

- **Trimming**: Enabled with partial mode for smaller binaries (~35MB vs framework-dependent)
- **Alpine Linux**: Uses `aspnet:10.0-alpine` base image (~100MB smaller than standard)
- **Connection Pooling**: Optimized HttpClient configuration for high performance
- **Speed Optimization**: Configured for runtime performance over build time

**Build Commands:**
```bash
# Development build
dotnet run

# Production build (optimized)
dotnet publish -c Release

# Container build (includes all optimizations)
docker build -t easyauth-proxy .
```

See `BUILD-OPTIMIZATIONS.md` for detailed performance analysis and additional optimization options.