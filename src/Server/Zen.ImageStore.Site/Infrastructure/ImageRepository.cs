using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.WindowsAzure.Storage.Blob;
using Zen.ImageStore.Site.Domain.Interfaces;

namespace Zen.ImageStore.Site.Infrastructure
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// <para>
    /// Images are stored using Azure Storage as block blobs.
    /// We support straight upload of a blob payload as a single stream
    /// or as chunked block transfer.
    /// When using chunked transfer, the chunks can be delivered in any
    /// order. When all chunks have been sent then the entire collection
    /// is committed. At our API edge we support the optional (and recommended)
    /// hashing of each chunk to verify transmission.
    /// </para>
    /// <para>
    /// There is always a container called "default" that contains all
    /// uploaded images.
    /// Additional containers may be created that correspond to albums.
    /// Within each container, the path to the blob always as follows;
    /// year/month/day/type/filename
    /// 
    /// The following "types" are supported;
    /// raw
    /// jpeg
    /// 
    /// To support changes to a blob we make use of blob snapshots to maintain
    /// each version of a blob
    /// 
    /// Finally we make use of metadata headers to store EXIF information.
    /// </para>
    /// </remarks>
    public class ImageRepository : IImageRepository
    {
        private readonly IStorageClientFactory _storageClientFactory;
        private readonly IMemoryCache _memoryCache;

        public ImageRepository(IStorageClientFactory storageClientFactory, IMemoryCache memoryCache)
        {
            _storageClientFactory = storageClientFactory;
            _memoryCache = memoryCache;
        }

        public async Task<ICollection<string>> ListImageContainersAsync(CancellationToken cancellationToken)
        {
            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            return blobClient.ListContainers()
                .Where(c => c.Name != "default")
                .Select(c => c.Name)
                .ToList();
        }

        public async Task UploadEntireImageAsync(
            string container, string pathname, Stream imageContent, CancellationToken cancellationToken)
        {
            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            var containerReference = blobClient.GetContainerReference(container);
            if (!containerReference.CreateIfNotExists())
            {
                throw new ArgumentException("Failed to create container");
            }

            // If blob exists then create a snapshot of the current blob first
            var blobRef = containerReference.GetBlockBlobReference(pathname);
            if (await blobRef.ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                await blobRef.CreateSnapshotAsync(cancellationToken).ConfigureAwait(false);
            }

            // Upload the image to the base blob
            await blobRef.UploadFromStreamAsync(imageContent, cancellationToken).ConfigureAwait(false);
        }

        public async Task BeginUploadChunkedImageAsync(
            string container, string pathname, CancellationToken cancellationToken)
        {
            // Chunks can be a maximum of 1MB in size (this is an imposed limit to ensure we have good feedback in the UI even with crappy connections)
            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            var containerReference = blobClient.GetContainerReference(container);
            if (!containerReference.CreateIfNotExists())
            {
                throw new ArgumentException("Failed to create container");
            }

            // If blob exists then create a snapshot of the current blob first
            var blobRef = containerReference.GetBlockBlobReference(pathname);
            if (await blobRef.ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                await blobRef.CreateSnapshotAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task UploadChunkedImageAsync(
            string container, string pathname, string chunkId, Stream content, string contentMd5, CancellationToken cancellationToken)
        {
            // Chunks can be a maximum of 1MB in size (this is an imposed limit to ensure we have good feedback in the UI even with crappy connections)
            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            var containerReference = blobClient.GetContainerReference(container);
            if (!await containerReference.ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Container does not exist");
            }

            // Upload blob block
            var blobRef = containerReference.GetBlockBlobReference(pathname);
            await blobRef
                .PutBlockAsync(
                    chunkId,
                    content,
                    contentMd5,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task CommitUploadChunkedImageAsync(
            string container, string pathname, string[] chunkIds, CancellationToken cancellationToken)
        {
            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            var containerReference = blobClient.GetContainerReference(container);
            if (!await containerReference.ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Container does not exist");
            }

            // Get blob reference and commit new block list
            var blobRef = containerReference.GetBlockBlobReference(pathname);
            await blobRef
                .PutBlockListAsync(chunkIds, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IImageEntryCollection> ListImagesAsync(
            string container, string pathname, Guid continuationId, int pageSize, CancellationToken cancellationToken)
        {
            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            var containerReference = blobClient.GetContainerReference(container);
            if (!await containerReference.ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Container does not exist");
            }

            // Create new continuation identifier
            if (continuationId == Guid.Empty)
            {
                continuationId = Guid.NewGuid();
            }

            var continuationToken = _memoryCache
                .Get<BlobContinuationToken>($"ISIC:{continuationId}");
            var results = await containerReference
                .ListBlobsSegmentedAsync(
                    pathname,
                    false,
                    BlobListingDetails.None,
                    pageSize,
                    continuationToken,
                    null,
                    null,
                    cancellationToken)
                .ConfigureAwait(false);

            // Save the continuation token in the cache if needed

            // Update the cache entry if necessary
            if (results.ContinuationToken != null)
            {
                _memoryCache.Set<BlobContinuationToken>(
                    $"ISIC:{continuationId}",
                    results.ContinuationToken,
                    DateTimeOffset.UtcNow.AddHours(1));
            }
            else
            {
                _memoryCache.Remove($"ISIC:{continuationId}");
                continuationId = Guid.Empty;
            }

            // Return transformed blob result object
            return
                new ImageEntryCollection
                {
                    ContinuationId = continuationId,
                    Images = results.Results
                        .Select(i =>
                            new ImageEntry
                            {
                                ContainerName = i.Container.Name,
                                FolderPrefix = i.Parent?.Prefix ?? string.Empty,
                                PrimaryUri = i.StorageUri.PrimaryUri,
                                SecondaryUri = i.StorageUri.SecondaryUri
                            })
                        .ToList()
                        .AsReadOnly()
                };
        }

        public async Task<string> CopyImageAsync(
            string sourceContainer, string pathname, string targetContainer, CancellationToken cancellationToken)
        {
            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            var sourceContainerReference = blobClient.GetContainerReference(sourceContainer);
            if (!await sourceContainerReference.ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Source container does not exist");
            }
            var targetContainerReference = blobClient.GetContainerReference(targetContainer);
            if (!await targetContainerReference.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Cannot create target container");
            }

            var sourceBlobRef = sourceContainerReference.GetBlobReference(pathname);
            var targetBlobRef = targetContainerReference.GetBlobReference(pathname);
            return await targetBlobRef
                .StartCopyAsync(sourceBlobRef.Uri, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task AbortCopyImageAsync(
            string container, string pathname, string copyId, CancellationToken cancellationToken)
        {
            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            var containerReference = blobClient.GetContainerReference(container);
            if (!await containerReference.ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Container does not exist");
            }

            // Abort the copy operation
            var blobRef = containerReference.GetBlobReference(pathname);
            await blobRef
                .AbortCopyAsync(copyId, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task DeleteImageAsync(string container, string pathname, CancellationToken cancellationToken)
        {
            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            var containerReference = blobClient.GetContainerReference(container);
            if (!await containerReference.ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Container does not exist");
            }

            // Abort the copy operation
            var blobRef = containerReference.GetBlobReference(pathname);
            await blobRef
                .DeleteIfExistsAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
