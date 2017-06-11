using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Zen.ImageStore.Site.Infrastructure;

namespace Zen.ImageStore.Site.Controllers
{
    [Route("api/[controller]")]
    public class AlbumsController : Controller
    {
        private readonly IImageRepository _imageRepository;

        public AlbumsController(IImageRepository imageRepository)
        {
            _imageRepository = imageRepository;
        }

        // GET: api/albums
        [HttpGet]
        [Route("")]
        public async Task<IEnumerable<string>> GetAsync(CancellationToken cancellationToken)
        {
            return await _imageRepository
                .ListImageContainersAsync(cancellationToken)
                .ConfigureAwait(true);
        }

        // GET api/values/5
        [HttpGet]
        [Route("{album}")]
        public async Task<IEnumerable<string>> GetAlbumImagesAsync(
            CancellationToken cancellationToken,
            string album,
            [FromQuery]string pathname)
        {
            var images = await _imageRepository
                .ListImagesAsync(album, pathname, Guid.Empty, 5000, cancellationToken)
                .ConfigureAwait(true);
            return null;
        }

        [HttpGet]
        public async Task<IActionResult> GetAlbumImageAsync(
            CancellationToken cancellationToken,
            string album, string pathname,
            int? width = null,
            int? height = null,
            bool? preserveAspectRatio = true)
        {
            // Create temporary stream and pull content from blob
            var memoryStream = new MemoryStream();
            await _imageRepository
                .GetImageStreamAsync(album, pathname, memoryStream, cancellationToken)
                .ConfigureAwait(true);

            // Load image into GDI object
            using (var originalImage = Bitmap.FromStream(memoryStream))
            {
                var renderImage = 
            }
                return File()
        }

        [HttpPost]
        [Route("{album}/{pathname}")]
        public async Task<IActionResult> PostEntireImageAsync(
            CancellationToken cancellationToken, string album, string pathname)
        {
            var imageContent = Request.Body;
            await _imageRepository
                .UploadEntireImageAsync(
                    album, pathname, imageContent, cancellationToken)
                .ConfigureAwait(true);
            return CreatedAtAction(
                "GetAlbumImage",
                new { album, pathname },
                "");
        }
    }
}
