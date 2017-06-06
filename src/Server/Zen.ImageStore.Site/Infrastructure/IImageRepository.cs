﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Zen.ImageStore.Site.Infrastructure
{
    public interface IImageRepository
    {
        Task UploadEntireImageAsync(string container, string pathname, Stream imageContent, CancellationToken cancellationToken);

        Task BeginUploadChunkedImageAsync(string container, string pathname, CancellationToken cancellationToken);

        Task UploadChunkedImageAsync(string container, string pathname, string chunkId, Stream content, string contentMD5, CancellationToken cancellationToken);

        Task CommitUploadChunkedImageAsync(string container, string pathname, string[] chunkIds, CancellationToken cancellationToken);

        Task<IImageEntryCollection> ListImagesAsync(string container, string pathname, Guid continuationId, int pageSize, CancellationToken cancellationToken);

        Task<string> CopyImageAsync(string sourceContainer, string pathname, string targetContainer, CancellationToken cancellationToken);

        Task AbortCopyImageAsync(string container, string pathname, string copyId, CancellationToken cancellationToken);

        Task DeleteImageAsync(string container, string pathname, CancellationToken cancellationToken);
    }
}