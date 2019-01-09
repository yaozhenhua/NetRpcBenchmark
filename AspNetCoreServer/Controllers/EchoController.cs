using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreServer.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class EchoController : ControllerBase
    {
        // GET echo
        [HttpGet("{input}")]
        public ActionResult<string> Echo(string input)
        {
            return input + "\r\n";
        }
    }
}
