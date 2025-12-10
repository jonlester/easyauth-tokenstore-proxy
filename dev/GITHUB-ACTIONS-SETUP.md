# GitHub Actions Setup for Azure Container Registry

This repository includes automated CI/CD to build and deploy the container image to Azure Container Registry.

## Required GitHub Secrets

Configure these secrets in your GitHub repository settings (`Settings` → `Secrets and variables` → `Actions`):

### Azure Container Registry Secrets

```
ACR_LOGIN_SERVER    # e.g., myregistry.azurecr.io
ACR_USERNAME        # Service principal client ID or admin username
ACR_PASSWORD        # Service principal client secret or admin password
```

## Setup Instructions

### 1. Create Azure Container Registry

```bash
# Create resource group (if needed)
az group create --name rg-easyauth-proxy --location eastus2

# Create container registry
az acr create \
  --resource-group rg-easyauth-proxy \
  --name myeasyauthregistry \
  --sku Basic \
  --admin-enabled true

# Get login server
az acr show --name myeasyauthregistry --query loginServer --output tsv

# Get admin credentials
az acr credential show --name myeasyauthregistry
```

### 2. Configure GitHub Secrets

Using Azure CLI (recommended for service principal):

```bash
# Create service principal for GitHub Actions
az ad sp create-for-rbac \
  --name "github-actions-acr" \
  --role "AcrPush" \
  --scopes /subscriptions/{subscription-id}/resourceGroups/rg-easyauth-proxy/providers/Microsoft.ContainerRegistry/registries/myeasyauthregistry \
  --sdk-auth

# Output will include:
# {
#   "clientId": "...",        # Use as ACR_USERNAME
#   "clientSecret": "...",    # Use as ACR_PASSWORD
#   "subscriptionId": "...",
#   "tenantId": "..."
# }
```

Or using admin credentials (simpler but less secure):

```bash
# Enable admin user (if not already enabled)
az acr update --name myeasyauthregistry --admin-enabled true

# Get credentials
az acr credential show --name myeasyauthregistry
# Use username as ACR_USERNAME and password as ACR_PASSWORD
```

### 3. Set GitHub Secrets

In your GitHub repository:

1. Go to `Settings` → `Secrets and variables` → `Actions`
2. Click `New repository secret`
3. Add each secret:

   - **ACR_LOGIN_SERVER**: `myeasyauthregistry.azurecr.io`
   - **ACR_USERNAME**: Service principal client ID or admin username
   - **ACR_PASSWORD**: Service principal client secret or admin password

## Workflow Features

### 🏗️ **Build Triggers**
- **Push to main/develop**: Full build and deploy
- **Pull requests to main**: Build only (no deploy)
- **Path filtering**: Only triggers on changes to `src/` or workflow files

### 🏷️ **Image Tagging Strategy**
- `latest` - Latest main branch build
- `main-{sha}` - Main branch with commit SHA
- `develop-{sha}` - Develop branch with commit SHA  
- `pr-{number}` - Pull request builds

### 🚀 **Optimization Features**
- **Multi-stage caching**: GitHub Actions cache for faster builds
- **Platform targeting**: linux/amd64 optimized
- **Buildx**: Advanced Docker build features

### 🔒 **Security Features**
- **Vulnerability scanning**: Trivy scanner on all pushes
- **Container scanning**: Azure ACR vulnerability assessment
- **SARIF upload**: Security results in GitHub Security tab
- **Service principal**: Least-privilege Azure access

## Usage Examples

### Viewing Built Images

```bash
# List images in registry
az acr repository list --name myeasyauthregistry

# Show tags for the image
az acr repository show-tags --name myeasyauthregistry --repository easyauth-tokenstore-proxy

# Pull specific image
docker pull myeasyauthregistry.azurecr.io/easyauth-tokenstore-proxy:latest
```

### Local Testing with ACR Image

```bash
# Login to ACR
az acr login --name myeasyauthregistry

# Run container from ACR
docker run -p 8080:8080 \
  -e BLOB_PRIMARY_CONTAINER_URL="https://..." \
  -e BLOB_SECONDARY_READ_CONTAINER_URL="https://..." \
  myeasyauthregistry.azurecr.io/easyauth-tokenstore-proxy:latest
```

### Deploying to Azure App Service

```bash
# Deploy sidecar from ACR to existing App Service
az webapp config container set \
  --resource-group rg-main-app \
  --name my-main-app \
  --sidecar-containers '[{
    "name": "blob-proxy",
    "image": "myeasyauthregistry.azurecr.io/easyauth-tokenstore-proxy:latest",
    "resources": {"cpu": 0.5, "memory": "1Gi"},
    "environmentVariables": [
      {"name": "BLOB_PRIMARY_CONTAINER_URL", "secureValue": "https://..."},
      {"name": "BLOB_SECONDARY_READ_CONTAINER_URL", "secureValue": "https://..."}
    ]
  }]'
```

## Troubleshooting

### Common Issues

1. **Authentication failed**: Check ACR credentials in GitHub secrets
2. **Build context errors**: Ensure Dockerfile references are correct for new folder structure
3. **Registry not found**: Verify ACR_LOGIN_SERVER format (include .azurecr.io)

### Monitoring Builds

- **GitHub Actions tab**: View build logs and status
- **Security tab**: Review vulnerability scan results
- **ACR portal**: Monitor registry usage and scan results

### Security Scan Results

The workflow includes two security scanning layers:

1. **Trivy**: Open source vulnerability scanner (results in GitHub Security tab)
2. **Azure Container Registry**: Built-in vulnerability assessment

Both scans help ensure your container images are secure before deployment.

## Cost Optimization

- **Basic SKU**: Suitable for development/small production workloads
- **Build caching**: Reduces build times and resource usage
- **Conditional scans**: Security scans only on push events
- **Path filtering**: Avoids unnecessary builds on doc-only changes