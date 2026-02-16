using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.ApiClient.CloudEvent.Controllers
{
    [Route("api/events")]
    [ApiController]
    public class CloudEventController : ControllerBase
    {
        [HttpPost("receive")]
        public ActionResult ReceiveCloudEvent([FromBody] CloudNative.CloudEvents.CloudEvent cloudEvent)
        {
            var attributeMap = new JsonObject();
            foreach (var (attribute, value) in cloudEvent.GetPopulatedAttributes())
            {
                attributeMap[attribute.Name] = attribute.Format(value);
            }
            return Ok($"Received event with ID {cloudEvent.Id}, attributes: {attributeMap}");
        }
    }
}
