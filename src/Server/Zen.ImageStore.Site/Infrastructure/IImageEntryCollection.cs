using System;
using System.Collections.Generic;

namespace Zen.ImageStore.Site.Infrastructure
{
    public interface IImageEntryCollection
    {
        Guid ContinuationId { get; }

        IList<ImageEntry> Images { get; }
    }
}