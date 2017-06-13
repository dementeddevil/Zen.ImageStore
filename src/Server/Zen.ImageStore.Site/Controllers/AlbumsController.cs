using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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

        [HttpGet]
        [Route("")]
        public async Task<IActionResult> GetAlbumsAsync(
            CancellationToken cancellationToken)
        {
            var albums = await _imageRepository
                .ListImageContainersAsync(cancellationToken)
                .ConfigureAwait(true);
            return Ok(albums);
        }

        [HttpDelete]
        [Route("{album}")]
        public async Task<IActionResult> DeleteAlbumAsync(
            CancellationToken cancellationToken, string album)
        {
            await _imageRepository
                .DeleteImageContainerAsync(album, cancellationToken)
                .ConfigureAwait(true);
            return Ok();
        }

        [HttpPost]
        [Route("{album}/{pathname}")]
        public async Task<IActionResult> PostEntireImageAsync(
            CancellationToken cancellationToken, string album, string pathname)
        {
            var imageContent = Request.Body;
            await _imageRepository
                .UploadEntireImageAsync(
                    album,
                    pathname,
                    imageContent,
                    Request.ContentType,
                    cancellationToken)
                .ConfigureAwait(true);
            return CreatedAtAction(
                "GetAlbumImageAsync",
                new { album, pathname },
                "");
        }

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
        [Route("{album}/{pathname}", Name = "GetAlbumImageAsync")]
        public async Task<IActionResult> GetAlbumImageAsync(
            CancellationToken cancellationToken,
            string album, string pathname,
            [FromQuery] int? width = null,
            [FromQuery] int? height = null)
        {
            // Create temporary stream and pull content from blob
            var sourceImageStream = new MemoryStream();
            var blobInfo = await _imageRepository
                .GetImageStreamAsync(album, pathname, sourceImageStream, cancellationToken)
                .ConfigureAwait(true);
            sourceImageStream.Position = 0;

            // Load image into GDI object
            var originalImage = Image.FromStream(sourceImageStream);

            // Resize image based on parameters
            var desiredSize = new Size(originalImage.Width, originalImage.Height);
            if (width.HasValue && width != desiredSize.Width &&
                height.HasValue && height != desiredSize.Height)
            {
                desiredSize = new Size(width.Value, height.Value);
            }
            else if (width.HasValue && width != desiredSize.Width ||
                height.HasValue && height != desiredSize.Height)
            {
                desiredSize = new Size(
                    width.GetValueOrDefault(0),
                    height.GetValueOrDefault(0));

                if (width.HasValue)
                {
                    var factor =
                        ((double) width.Value) /
                        ((double) originalImage.Width);
                    desiredSize.Height = (int)(((double) originalImage.Height) * factor);
                }
                else
                {
                    var factor =
                        ((double)height.Value) /
                        ((double)originalImage.Height);
                    desiredSize.Width = (int)(((double)originalImage.Width) * factor);
                }
            }

            // Determine image we will render to the client
            var renderImage = originalImage;
            if (desiredSize.Width != originalImage.Width ||
                desiredSize.Height != originalImage.Height)
            {
                renderImage = originalImage
                    .GetThumbnailImage(
                        desiredSize.Width, desiredSize.Height,
                        () => cancellationToken.IsCancellationRequested,
                        IntPtr.Zero);
            }

            // Save image to temporary stream
            var targetImageStream = new MemoryStream();
            renderImage.Save(targetImageStream, GetImageFormatFrom(pathname));
            originalImage.Dispose();
            renderImage.Dispose();

            return File(targetImageStream, blobInfo.ContentType, Path.GetFileName(pathname));
        }

        private ImageFormat GetImageFormatFrom(string pathname)
        {
            var extn = Path.GetExtension(pathname).ToLower();
            switch (extn)
            {
                case ".bmp":
                    return ImageFormat.Bmp;
                case ".png":
                    return ImageFormat.Png;
                case ".gif":
                    return ImageFormat.Gif;
                case ".jpg":
                case ".jpeg":
                    return ImageFormat.Jpeg;
                case ".tiff":
                    return ImageFormat.Tiff;
                case ".exif":
                    return ImageFormat.Exif;
                default:
                    return ImageFormat.Wmf;
            }
        }
    }
}
