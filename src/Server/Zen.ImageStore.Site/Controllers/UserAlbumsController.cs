using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.SwaggerGen.Annotations;
using Zen.ImageStore.Site.Infrastructure;

namespace Zen.ImageStore.Site.Controllers
{
    /// <summary>
    /// <c>UserAlbumsController</c> this api endpoint allows interaction with
    /// albums associated with any user caller and is designed to be used by
    /// clients that where a user has consented to their use.
    /// </summary>
    [Route("api/users/{userId:guid}/albums")]
    public class UserAlbumsController : Controller
    {
        private readonly IImageRepository _imageRepository;

        /// <summary>
        /// Constructs a new instance of the AlbumsController
        /// </summary>
        /// <param name="imageRepository">
        /// Injected instance of the image repository
        /// </param>
        public UserAlbumsController(IImageRepository imageRepository)
        {
            _imageRepository = imageRepository;
        }


        /// <summary>
        /// Gets the list of albums associated with the current caller
        /// </summary>
        /// <param name="cancellationToken">
        /// An object the framework can use to cancel this operation.
        /// </param>
        /// <param name="userId">Guid identifier associated with a given user.</param>
        /// <returns></returns>
        [HttpGet]
        [Route("")]
        public async Task<IActionResult> GetAlbumsAsync(
            CancellationToken cancellationToken,
            Guid userId)
        {
            // TODO: Create an additional method that can be passed a user id
            //  The new entry point would only be callable by API level clients
            //  and the user would need to have authorised the client for access
            var albums = await _imageRepository
                .ListImageContainersAsync(cancellationToken)
                .ConfigureAwait(true);
            return Ok(albums);
        }

        /// <summary>
        /// Deletes the specified album from the current caller's list of albums.
        /// </summary>
        /// <param name="cancellationToken">
        /// An object the framework can use to cancel this operation.
        /// </param>
        /// <param name="userId">Guid identifier associated with a given user.</param>
        /// <param name="album">The album identifier.</param>
        /// <returns>
        /// 200 If the album is deleted
        /// 
        /// </returns>
        [HttpDelete]
        [Route("{album}")]
        [SwaggerOperation()]
        public async Task<IActionResult> DeleteAlbumAsync(
            CancellationToken cancellationToken,
            Guid userId,
            string album)
        {
            await _imageRepository
                .DeleteImageContainerAsync(album, cancellationToken)
                .ConfigureAwait(true);
            return Ok();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken">
        /// An object the framework can use to cancel this operation.
        /// </param>
        /// <param name="userId">Guid identifier associated with a given user.</param>
        /// <param name="album"></param>
        /// <param name="pathname"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("{album}/{pathname}")]
        public async Task<IActionResult> PostEntireImageAsync(
            CancellationToken cancellationToken,
            Guid userId,
            string album,
            string pathname)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken">
        /// An object the framework can use to cancel this operation.
        /// </param>
        /// <param name="userId">Guid identifier associated with a given user.</param>
        /// <param name="album"></param>
        /// <param name="pathname"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("{album}/{pathname}/chunked")]
        public async Task<IActionResult> BeginPostChunkedImageAsync(
            CancellationToken cancellationToken,
            Guid userId,
            string album,
            string pathname)
        {
            // TODO: Pass request to underlying service
            // create cookie to remember this upload

            // TODO: Return new route with cookie
            return CreatedAtRoute(
                "PatchChunkedImage",
                new
                {
                    album,
                    pathname,
                },
                "");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken">
        /// An object the framework can use to cancel this operation.
        /// </param>
        /// <param name="userId">Guid identifier associated with a given user.</param>
        /// <param name="album"></param>
        /// <param name="pathname"></param>
        /// <param name="uploadId"></param>
        /// <param name="chunkId"></param>
        /// <returns></returns>
        [HttpPatch]
        [Route("{album}/{pathname}/chunked/{imageId}/{chunkId}")]
        public async Task<IActionResult> PatchChunkedImageAsync(
            CancellationToken cancellationToken,
            Guid userId,
            string album,
            string pathname,
            string uploadId,
            int chunkId)
        {
            // TODO: Pass stream to underlying service

            return Accepted();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken">
        /// An object the framework can use to cancel this operation.
        /// </param>
        /// <param name="userId">Guid identifier associated with a given user.</param>
        /// <param name="album"></param>
        /// <param name="pathname"></param>
        /// <param name="uploadId"></param>
        /// <param name="chunkIds"></param>
        /// <returns></returns>
        [HttpPatch]
        [Route("{album}/{pathname}/chunked/{imageId}")]
        public async Task<IActionResult> CompleteChunkedImageAsync(
            CancellationToken cancellationToken,
            Guid userId,
            string album,
            string pathname,
            string uploadId,
            int[] chunkIds)
        {
            // TODO: Complete upload using the specified chunks

            return CreatedAtRoute(
                "GetAlbumImage",
                new
                {
                    album,
                    pathname
                },
                "");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken">
        /// An object the framework can use to cancel this operation.
        /// </param>
        /// <param name="userId">Guid identifier associated with a given user.</param>
        /// <param name="album"></param>
        /// <param name="pathname"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{album}")]
        public async Task<IEnumerable<string>> GetAlbumImagesAsync(
            CancellationToken cancellationToken,
            Guid userId,
            string album,
            [FromQuery]string pathname)
        {
            var images = await _imageRepository
                .ListImagesAsync(album, pathname, Guid.Empty, 5000, cancellationToken)
                .ConfigureAwait(true);
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken">
        /// An object the framework can use to cancel this operation.
        /// </param>
        /// <param name="userId">Guid identifier associated with a given user.</param>
        /// <param name="album"></param>
        /// <param name="pathname"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{album}/{pathname}", Name = "GetAlbumImageAsync")]
        public async Task<IActionResult> GetAlbumImageAsync(
            CancellationToken cancellationToken,
            Guid userId, string album, string pathname,
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
