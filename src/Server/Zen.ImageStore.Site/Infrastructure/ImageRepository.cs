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
        public const string DefaultContainerName = "default";

        private readonly IStorageClientFactory _storageClientFactory;
        private readonly IMemoryCache _memoryCache;

        public ImageRepository(IStorageClientFactory storageClientFactory, IMemoryCache memoryCache)
        {
            _storageClientFactory = storageClientFactory;
            _memoryCache = memoryCache;
        }

        /// <summary>
        /// Gets a list of album names for the current caller
        /// </summary>
        /// <param name="cancellationToken">
        /// Token supplied by the runtime to control the cancellation of this request.
        /// </param>
        /// <returns></returns>
        public async Task<ICollection<string>> ListImageContainersAsync(CancellationToken cancellationToken)
        {
            var results = new List<string>();

            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            BlobContinuationToken continuationToken = null;
            do
            {
                // Get the next block of containers
                var containers = await blobClient
                    .ListContainersSegmentedAsync(continuationToken, cancellationToken)
                    .ConfigureAwait(false);
                continuationToken = containers.ContinuationToken;

                results.AddRange(containers.Results.Select(c => c.Name));
            } while (continuationToken != null);

            return results.Where(c => !string.Equals(c, DefaultContainerName, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>
        /// Deletes an album from the current caller's collection
        /// </summary>
        /// <param name="container"></param>
        /// <param name="cancellationToken">
        /// Token supplied by the runtime to control the cancellation of this request.
        /// </param>
        /// <returns></returns>
        public async Task DeleteImageContainerAsync(string container, CancellationToken cancellationToken)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }
            if (string.Equals(container, DefaultContainerName, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Cannot delete default container");
            }

            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            var containerReference = blobClient.GetContainerReference(container);
            await containerReference.DeleteIfExistsAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task UploadEntireImageAsync(
            string container, string pathname, Stream imageContent, string contentType, CancellationToken cancellationToken)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

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
            else
            {
                blobRef.Properties.ContentType = contentType;
            }

            // Upload the image to the base blob
            await blobRef.UploadFromStreamAsync(imageContent, cancellationToken).ConfigureAwait(false);
        }

        public async Task BeginUploadChunkedImageAsync(
            string container, string pathname, CancellationToken cancellationToken)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            // Chunks can be a maximum of 1MB in size (this is an imposed limit to ensure we have good feedback in the UI even with crappy connections)
            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            var containerReference = blobClient.GetContainerReference(container);
            if (!containerReference.CreateIfNotExists())
            {
                throw new ArgumentException("Failed to create container");
            }

            var blobRef = containerReference.GetBlockBlobReference(pathname);
            if (await blobRef.ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                await blobRef.CreateSnapshotAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task UploadChunkedImageAsync(
            string container, string pathname, string chunkId, Stream content, string contentMd5, CancellationToken cancellationToken)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            // Chunks can be a maximum of 1MB in size (this is an imposed limit to ensure we have good feedback in the UI even with crappy connections)
            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            var containerReference = blobClient.GetContainerReference(container);
            if (!await containerReference.ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Container does not exist");
            }

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
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            var containerReference = blobClient.GetContainerReference(container);
            if (!await containerReference.ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Container does not exist");
            }

            var blobRef = containerReference.GetBlockBlobReference(pathname);
            await blobRef
                .PutBlockListAsync(chunkIds, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IImageEntryCollection> ListImagesAsync(
            string container, string pathname, Guid continuationId, int pageSize, CancellationToken cancellationToken)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

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
                                SecondaryUri = i.StorageUri.SecondaryUri,
                            })
                        .ToList()
                        .AsReadOnly()
                };
        }

        public async Task<IImageEntry> GetImageAsync(string container, string pathname, CancellationToken cancellationToken)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            var containerReference = blobClient.GetContainerReference(container);
            if (!await containerReference.ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Container does not exist");
            }

            var blobRef = containerReference.GetBlobReference(pathname);
            return new ImageEntry
            {
                ContainerName = containerReference.Name,
                FolderPrefix = blobRef.Parent.Prefix,
                PrimaryUri = blobRef.StorageUri.PrimaryUri,
                SecondaryUri = blobRef.StorageUri.SecondaryUri,
                ContentType = blobRef.Properties.ContentType
            };
        }

        public async Task<IImageEntry> GetImageStreamAsync(string container, string pathname, Stream stream, CancellationToken cancellationToken)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            var containerReference = blobClient.GetContainerReference(container);
            if (!await containerReference.ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Container does not exist");
            }

            var blobRef = containerReference.GetBlobReference(pathname);
            await blobRef
                .DownloadToStreamAsync(stream, cancellationToken)
                .ConfigureAwait(false);
            return new ImageEntry
            {
                ContainerName = containerReference.Name,
                FolderPrefix = blobRef.Parent.Prefix,
                PrimaryUri = blobRef.StorageUri.PrimaryUri,
                SecondaryUri = blobRef.StorageUri.SecondaryUri,
                ContentType = blobRef.Properties.ContentType
            };
        }

        public async Task<string> CopyImageAsync(
            string sourceContainer, string pathname, string targetContainer, CancellationToken cancellationToken)
        {
            if (sourceContainer == null)
            {
                throw new ArgumentNullException(nameof(sourceContainer));
            }
            if (targetContainer == null)
            {
                throw new ArgumentNullException(nameof(targetContainer));
            }
            if (string.Equals(sourceContainer, targetContainer, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Target container cannot be same as source.", nameof(targetContainer));
            }
            if (string.Equals(targetContainer, DefaultContainerName, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Target container cannot be default.", nameof(targetContainer));
            }

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
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }
            if (string.Equals(container, DefaultContainerName, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Container cannot be default.", nameof(container));
            }

            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            var containerReference = blobClient.GetContainerReference(container);
            if (!await containerReference.ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Container does not exist");
            }

            var blobRef = containerReference.GetBlobReference(pathname);
            await blobRef
                .AbortCopyAsync(copyId, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task DeleteImageAsync(string container, string pathname, CancellationToken cancellationToken)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            var blobClient = await _storageClientFactory.CreateBlobClientAsync().ConfigureAwait(false);
            var containerReference = blobClient.GetContainerReference(container);
            if (!await containerReference.ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Container does not exist");
            }

            var blobRef = containerReference.GetBlobReference(pathname);
            await blobRef
                .DeleteIfExistsAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
