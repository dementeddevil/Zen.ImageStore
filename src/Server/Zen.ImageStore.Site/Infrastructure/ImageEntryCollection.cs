using System;
using System.Collections.Generic;

namespace Zen.ImageStore.Site.Infrastructure
{
    public class ImageEntryCollection : IImageEntryCollection
    {
        public Guid ContinuationId { get; set; }

        public IList<ImageEntry> Images { get; set; }
    }
}
