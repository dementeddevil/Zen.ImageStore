using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

namespace Zen.ImageStore.Site.Domain.Interfaces
{
    public interface IStorageClientFactory
    {
        Task<CloudBlobClient> CreateBlobClientAsync();

        Task<CloudTableClient> CreateTableClientAsync();
    }
}