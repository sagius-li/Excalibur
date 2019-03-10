using Microsoft.AspNetCore.Mvc;

namespace OCG.DataService.PluginExample
{
    /// <summary>
    /// Plugin example to show how application parts work
    /// </summary>
    [Route("api/plugin/examples")]
    [ApiController]
    public class PluginExample : ControllerBase
    {
        /// <summary>
        /// Plugin example to show how application parts work
        /// </summary>
        /// <param name="message">Message to show in the return value. Please check information about implement plugin using <a href="https://codereview.stackexchange.com/questions/189841/load-asp-net-core-plugins-and-their-dependencies" target="_blank">application parts</a></param>
        /// <returns></returns>
        [HttpGet("{message}")]
        public ActionResult Get([FromRoute] string message)
        {
            return Ok($"{message} - from plugin example");
        }
    }
}
