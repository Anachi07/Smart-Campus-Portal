using Microsoft.AspNetCore.Mvc;
using SmartQueueSystemGUI.Services;
namespace SmartQueueSystemGUI.Controllers;
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class VerifyController(QueueStore store):Controller {
 public IActionResult Document(string code){if(code==null||!System.Text.RegularExpressions.Regex.IsMatch(code,"^[a-f0-9]{32}$"))return NotFound();var doc=store.VerifyIssuedDocument(code);return doc==null?NotFound():View(doc);}
}
