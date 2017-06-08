using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Zen.ImageStore.Site.Infrastructure;

namespace Zen.ImageStore.Site.Controllers
{
    [Route("api/[controller]")]
    public class ImagesApiController : Controller
    {
        private readonly IImageRepository _imageRepository;

        public ImagesApiController(IImageRepository imageRepository)
        {
            _imageRepository = imageRepository;
        }

        // GET: api/values
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody]string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
