using System;
using System.Collections.Generic;
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
        [HttpGet("{album}")]
        public async Task<IEnumerable<string>> GetAlbumImagesAsync(

            string album,
            [FromQuery]string pathname,
            CancellationToken cancellationToken)
        {
            var images = await _imageRepository
                .ListImagesAsync(album, pathname, Guid.Empty, 5000, cancellationToken)
                .ConfigureAwait(true);
            return null;
        }
    }
}
