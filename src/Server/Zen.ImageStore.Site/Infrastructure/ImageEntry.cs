using System;

namespace Zen.ImageStore.Site.Infrastructure
{
    public class ImageEntry : IImageEntry
    {
        public string ContainerName { get; set; }

        public string FolderPrefix { get; set; }

        public Uri PrimaryUri { get; set; }

        public Uri SecondaryUri { get; set; }

        public string ContentType { get; set; }
    }
}
