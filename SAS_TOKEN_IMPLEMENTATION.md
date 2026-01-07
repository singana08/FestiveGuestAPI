# Azure Blob Storage SAS Token Implementation

## Overview
This implementation provides secure file access using SAS (Shared Access Signature) tokens for Azure Blob Storage, eliminating the need for public blob access.

## Architecture

### Backend (.NET Core API)

#### 1. StorageController
- **Endpoint**: `GET /api/storage/get-sas-url/{fileName}`
- **Endpoint**: `GET /api/storage/get-sas-url?fileName=xxx&container=yyy`
- **Purpose**: Generates read-only SAS tokens for blob access
- **Access**: Anonymous (no authentication required)

#### 2. FileUploadService
- **Method**: `GenerateReadSasUrl(fileName, containerName)`
- **Returns**: SAS URL valid for 1 hour
- **Permissions**: Read-only

#### 3. Configuration
Add to `appsettings.json`:
```json
{
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=festivegueststorage;AccountKey=YOUR_KEY;EndpointSuffix=core.windows.net"
  }
}
```

### Frontend (React)

#### 1. storageService.js
- Fetches SAS URLs from API
- Implements caching (50-minute expiry)
- Provides fallback to direct URLs

#### 2. Logo Component
- Dynamically loads logos using SAS URLs
- Falls back to local assets on error
- Shows loading state

#### 3. GuestDashboard
- Fetches SAS URLs for all profile images
- Caches URLs in component state
- Displays images securely

## Usage

### Backend Setup

1. **Install NuGet Packages** (already installed):
```bash
dotnet add package Azure.Storage.Blobs
dotnet add package Azure.Storage.Common
```

2. **Update appsettings.json**:
Replace `YOUR_KEY` with your actual Azure Storage account key.

3. **Deploy API**:
The StorageController is automatically registered.

### Frontend Usage

#### For Static Assets (Logos):
```javascript
import storageService from '../utils/storageService';

const url = await storageService.getSasUrl('festive-logo.png', 'logos');
```

#### For Profile Images:
```javascript
const fileName = profileImageUrl.split('/').pop();
const sasUrl = await storageService.getSasUrl(fileName, 'profile-images');
```

## Security Benefits

✅ **Private Containers**: Blob containers can remain private  
✅ **Time-Limited Access**: SAS tokens expire after 1 hour  
✅ **Read-Only Permissions**: Tokens only grant read access  
✅ **Granular Control**: Per-file access control  
✅ **No Public Exposure**: Files not accessible without valid token  

## API Endpoints

### Get SAS URL (Path Parameter)
```http
GET /api/storage/get-sas-url/festive-logo.png
```

**Response**:
```json
{
  "url": "https://festivegueststorage.blob.core.windows.net/logos/festive-logo.png?sv=2021-06-08&se=..."
}
```

### Get SAS URL (Query Parameters)
```http
GET /api/storage/get-sas-url?fileName=festive-logo.png&container=logos
```

**Response**:
```json
{
  "url": "https://festivegueststorage.blob.core.windows.net/logos/festive-logo.png?sv=2021-06-08&se=..."
}
```

## Caching Strategy

- **Client-side caching**: 50 minutes (tokens valid for 60 minutes)
- **Cache key**: `{container}/{fileName}`
- **Cache invalidation**: Automatic on expiry
- **Manual clear**: `storageService.clearCache()`

## Testing

### Test Logo Loading:
1. Open the application
2. Check browser console for SAS URL requests
3. Verify logos load correctly

### Test Profile Images:
1. Navigate to Guest Dashboard
2. Verify host profile images load
3. Check Network tab for SAS URLs

## Troubleshooting

### Images Not Loading:
1. Check Azure Storage connection string in appsettings.json
2. Verify blob container names match ('logos', 'profile-images')
3. Check browser console for errors
4. Verify API endpoint is accessible

### SAS Token Errors:
1. Ensure storage account key is correct
2. Check system time is synchronized
3. Verify container exists in Azure Storage

## Future Enhancements

- [ ] Server-side caching with Redis
- [ ] CDN integration for better performance
- [ ] Batch SAS URL generation endpoint
- [ ] Upload SAS tokens for direct client uploads
- [ ] Image optimization service
