using Microsoft.AspNetCore.Mvc;
using QRCoder;
using System.Security.Cryptography;
using System.Text;
namespace SmartQueueSystemGUI.Controllers;
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class CampusToolsController:Controller
{
    [HttpPost]
    public IActionResult AttendanceQr(int session,string code)
    {
        if(HttpContext.Session.GetString("CampusRole") is not ("Teacher" or "Admin" or "Super Admin"))return StatusCode(403);
        var expected=HttpContext.Session.GetString("CampusCsrf")??"";
        var supplied=Request.Headers["X-Campus-Csrf"].ToString();
        if(expected.Length==0||!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected),Encoding.UTF8.GetBytes(supplied)))return BadRequest();
        if(session<1||code==null||!System.Text.RegularExpressions.Regex.IsMatch(code,"^[a-f0-9]{32}$"))return BadRequest();
        var url=$"{Request.Scheme}://{Request.Host}/sims/modules/shared/rotating_attendance.php?session={session}#code={code}";
        using var generator=new QRCodeGenerator();using var data=generator.CreateQrCode(url,QRCodeGenerator.ECCLevel.Q);
        return Content(new SvgQRCode(data).GetGraphic(6,"#104b2b","#ffffff",true),"image/svg+xml");
    }
}
