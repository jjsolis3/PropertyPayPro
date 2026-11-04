using Microsoft.Net.Http.Headers;

namespace PropertyPayPro.Infrastructure;

public static class InlineFileHeader
{
    /// <summary>
    /// Sets "Content-Disposition: inline" with the file name encoded safely. Writing
    /// the raw name into the header crashed (500) for names with accents, dashes or
    /// other non-ASCII characters ("José – recibo.pdf"), because HTTP headers must be
    /// ASCII. SetHttpFileName adds an ASCII fallback plus the UTF-8 filename* form.
    /// </summary>
    public static void Set(HttpResponse response, string fileName)
    {
        var cd = new ContentDispositionHeaderValue("inline");
        cd.SetHttpFileName(string.IsNullOrWhiteSpace(fileName) ? "file" : fileName);
        response.Headers.ContentDisposition = cd.ToString();
    }
}
