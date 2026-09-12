using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantPos.Api.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class PaymentStatusController : ControllerBase
{
    [HttpGet("/payment-status")]
    [Produces("text/html")]
    [ProducesResponseType(typeof(ContentResult), StatusCodes.Status200OK)]
    public ContentResult Get([FromQuery] string? status)
    {
        var isSuccess = string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);
        return Content(BuildPage(isSuccess), "text/html; charset=utf-8");
    }

    private static string BuildPage(bool isSuccess)
    {
        var title = isSuccess ? "Payment Successful" : "Payment Failed";
        var titleAr = isSuccess ? "تم الدفع بنجاح" : "فشلت عملية الدفع";
        var color = isSuccess ? "#047857" : "#b91c1c";

        return $$"""
            <!doctype html>
            <html lang="en" dir="auto">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>{{title}}</title>
            </head>
            <body style="margin:0;display:grid;min-height:100vh;place-items:center;font-family:system-ui,sans-serif;background:#f8fafc;color:{{color}}">
              <main style="padding:24px;text-align:center">
                <h1>{{title}}</h1>
                <p lang="ar" dir="rtl">{{titleAr}}</p>
              </main>
            </body>
            </html>
            """;
    }
}
