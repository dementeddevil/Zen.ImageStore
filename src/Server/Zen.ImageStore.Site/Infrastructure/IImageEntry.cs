using System;

namespace Zen.ImageStore.Site.Infrastructure
{
    public interface IImageEntry
    {
        string ContainerName { get; }

        string FolderPrefix { get; }

        Uri PrimaryUri { get; }

        Uri SecondaryUri { get; }
    }
}