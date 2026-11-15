namespace OpenTicket.API.Security;

internal static class SecurityHeadersMiddleware
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
                headers.CacheControl = "no-store";
            }
            else
            {
                // The SPA: scripts only from this origin. Angular component styles are injected inline, hence
                // 'unsafe-inline' for styles only. Fonts and icons come from Google Fonts.
                headers.ContentSecurityPolicy =
                    "default-src 'self'; script-src 'self'; " +
                    "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                    "font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self'; " +
                    "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
            }

            await next(context);
        });
}
