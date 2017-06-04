using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.Azure.KeyVault;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Zen.ImageStore.Site.Domain.Interfaces;

namespace Zen.ImageStore.Site.Infrastructure
{
    public class StorageClientFactory : IStorageClientFactory
    {
        public StorageClientFactory(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        private IConfiguration Configuration { get; }

        public async Task<CloudBlobClient> CreateBlobClientAsync()
        {
            var storageAccount = await GetStorageAccountAsync().ConfigureAwait(false);
            return storageAccount.CreateCloudBlobClient();
        }

        public async Task<CloudTableClient> CreateTableClientAsync()
        {
            var storageAccount = await GetStorageAccountAsync().ConfigureAwait(false);
            return storageAccount.CreateCloudTableClient();
        }

        private async Task<CloudStorageAccount> GetStorageAccountAsync()
        {
            var accountName = Configuration["Storage:AccountName"];
            var keyValue = await GetStorageKeyAsync().ConfigureAwait(false);
            return new CloudStorageAccount(new StorageCredentials(accountName, keyValue), true);
        }

        private async Task<string> GetStorageKeyAsync()
        {
            // Create keyvault client
            var client = new KeyVaultClient(
                GetAccessTokenAsync,
                new System.Net.Http.HttpClient());

            // Pull storage access key from the vault
            var vaultUrl = Configuration["Security:VaultUrl"];
            var secret = await client.GetSecretAsync(vaultUrl, "StorageAccessKey").ConfigureAwait(false);

            // Return secret value
            return secret.Value;
        }

        private async Task<string> GetAccessTokenAsync(
            string authority, string resource, string scope)
        {
            // Pull our AD credentials from configuration
            var clientId = Configuration["Authentication:AzureAd:ClientId"];
            var clientSecret = Configuration["Authentication:AzureAd:ClientSecret"];
            var clientCredential = new ClientCredential(clientId, clientSecret);

            var context = new AuthenticationContext(authority, TokenCache.DefaultShared);
            var result = await context.AcquireTokenAsync(resource, clientCredential).ConfigureAwait(false);

            return result.AccessToken;
        }
    }
}
