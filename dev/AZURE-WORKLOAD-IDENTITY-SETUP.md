# Azure Workload Identity (OIDC) Setup for GitHub Actions

This guide shows how to set up delegated Entra ID authentication for GitHub Actions, eliminating the need for storing secrets.

## Benefits of Workload Identity

- ✅ **No secrets to manage**: No passwords or service principal secrets in GitHub
- ✅ **Enhanced security**: Time-bound tokens with automatic rotation
- ✅ **Audit trail**: All authentication events logged in Azure AD
- ✅ **Principle of least privilege**: Fine-grained access control

## Setup Instructions

### 1. Create Azure Resources

```bash
# Variables (customize these)
RESOURCE_GROUP="rg-easyauth-proxy"
ACR_NAME="myeasyauthregistry"
LOCATION="eastus2"
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
GITHUB_ORG="your-github-username"  # or organization name
GITHUB_REPO="easyauth-tokenstore-proxy"

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create container registry
az acr create \
  --resource-group $RESOURCE_GROUP \
  --name $ACR_NAME \
  --sku Basic
```

### 2. Create App Registration and Service Principal

```bash
# Create app registration for GitHub Actions
APP_ID=$(az ad app create \
  --display-name "github-actions-$GITHUB_REPO" \
  --query appId -o tsv)

# Create service principal
SP_ID=$(az ad sp create --id $APP_ID --query id -o tsv)

echo "Application (Client) ID: $APP_ID"
echo "Service Principal Object ID: $SP_ID"
```

### 3. Assign ACR Permissions

```bash
# Get ACR resource ID
ACR_ID=$(az acr show --name $ACR_NAME --resource-group $RESOURCE_GROUP --query id -o tsv)

# Assign AcrPush role (allows push/pull)
az role assignment create \
  --assignee $SP_ID \
  --role "AcrPush" \
  --scope $ACR_ID

# Verify assignment
az role assignment list --assignee $SP_ID --output table
```

### 4. Create Federated Credentials

Create federated credentials for main branch and pull requests:

```bash
# For main branch deployments
az ad app federated-credential create \
  --id $APP_ID \
  --parameters '{
    "name": "github-main",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:'$GITHUB_ORG'/'$GITHUB_REPO':ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# For develop branch deployments
az ad app federated-credential create \
  --id $APP_ID \
  --parameters '{
    "name": "github-develop", 
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:'$GITHUB_ORG'/'$GITHUB_REPO':ref:refs/heads/develop",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# For pull request builds
az ad app federated-credential create \
  --id $APP_ID \
  --parameters '{
    "name": "github-pr",
    "issuer": "https://token.actions.githubusercontent.com", 
    "subject": "repo:'$GITHUB_ORG'/'$GITHUB_REPO':pull_request",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

### 5. Configure GitHub Secrets

Use **Secrets** in GitHub repository settings (`Settings` → `Secrets and variables` → `Actions` → `Secrets` tab):

| Secret Name | Value | Example |
|-------------|-------|---------|
| `ACR_LOGIN_SERVER` | ACR login server | `myeasyauthregistry.azurecr.io` |
| `AZURE_CLIENT_ID` | Application (Client) ID | `12345678-1234-1234-1234-123456789012` |
| `AZURE_TENANT_ID` | Tenant ID | `87654321-4321-4321-4321-210987654321` |
| `AZURE_SUBSCRIPTION_ID` | Subscription ID | `11111111-2222-3333-4444-555555555555` |

**To get these values:**

```bash
# ACR login server
az acr show --name $ACR_NAME --query loginServer -o tsv

# Client ID (already displayed above as $APP_ID)
echo $APP_ID

# Tenant ID
az account show --query tenantId -o tsv

# Subscription ID  
az account show --query id -o tsv
```

## Verification

### Test the Setup

```bash
# Verify federated credentials
az ad app federated-credential list --id $APP_ID --output table

# Verify role assignments
az role assignment list --assignee $SP_ID --scope $ACR_ID --output table

# Test ACR access
az acr repository list --name $ACR_NAME
```

### Monitor Authentication

1. **Azure Portal**: `Azure Active Directory` → `Enterprise applications` → `github-actions-{repo}` → `Sign-ins`
2. **GitHub Actions**: Check workflow logs for authentication steps
3. **ACR Activity**: `Container registries` → `{registry}` → `Activity log`

## Security Best Practices

### Conditional Access Policies

```bash
# Create conditional access policy for GitHub Actions (optional)
# This restricts the service principal to only work from GitHub's IP ranges
```

### Monitoring and Alerting

```bash
# Set up alerts for unusual authentication patterns
az monitor activity-log alert create \
  --name "GitHub Actions Auth Alert" \
  --resource-group $RESOURCE_GROUP \
  --condition category=Administrative and operationName=Microsoft.Authorization/roleAssignments/write \
  --description "Alert when role assignments change for GitHub Actions SP"
```

### Regular Rotation

```bash
# Federated credentials don't expire, but you can rotate the app registration if needed
# Create new app registration
NEW_APP_ID=$(az ad app create --display-name "github-actions-$GITHUB_REPO-v2" --query appId -o tsv)

# Repeat setup steps with new app ID
# Update GitHub variables with new CLIENT_ID
# Delete old app registration when migration is complete
```

## Troubleshooting

### Common Issues

1. **Authentication failed**: Check federated credential `subject` matches exactly
2. **Permission denied**: Verify role assignments on ACR resource
3. **Token exchange failed**: Ensure `permissions: id-token: write` in workflow

### Debugging Commands

```bash
# Check app registration
az ad app show --id $APP_ID --query '{displayName:displayName, appId:appId}'

# List federated credentials
az ad app federated-credential list --id $APP_ID --output table

# Check service principal permissions
az role assignment list --assignee $SP_ID --all --output table
```

### GitHub Actions Debugging

Add this step to your workflow for debugging:

```yaml
- name: Debug Azure Context
  run: |
    az account show
    az acr list --output table
```

## Migration from Secrets

If you previously used secrets:

1. **Remove secrets**: Delete `ACR_USERNAME`, `ACR_PASSWORD` from GitHub secrets
2. **Add variables**: Add the four variables listed above  
3. **Update workflow**: Use the updated workflow file
4. **Test**: Run a workflow to verify OIDC authentication works
5. **Clean up**: Delete old service principal if using separate credentials

This setup provides enterprise-grade security with zero secrets management overhead!