using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PropertyPayPro.Services;

namespace PropertyPayPro.Infrastructure;

/// <summary>
/// Turns a <see cref="StoredFileMissingException"/> from any page handler (preview,
/// download, photo, avatar, ...) into a friendly "file not found" page with a 404,
/// instead of the generic 500 error page. The missing storage key is logged for
/// the admin but not shown to the visitor.
/// </summary>
public class StoredFileMissingFilter : IAsyncPageFilter
{
    private readonly ILogger<StoredFileMissingFilter> _logger;

    public StoredFileMissingFilter(ILogger<StoredFileMissingFilter> logger) => _logger = logger;

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var executed = await next();
        if (executed.ExceptionHandled || executed.Exception is not StoredFileMissingException missing) return;

        _logger.LogWarning("Stored file missing for {Path}: {StorageKey}",
            context.HttpContext.Request.Path, missing.StorageKey);
        executed.Result = FileMissingResult();
        executed.ExceptionHandled = true;
    }

    public static ContentResult FileMissingResult() => new()
    {
        StatusCode = StatusCodes.Status404NotFound,
        ContentType = "text/html; charset=utf-8",
        Content = """
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>File not found</title>
            <style>
              body { font-family: system-ui, -apple-system, "Segoe UI", sans-serif; background: #f6f6f6;
                     color: #333; margin: 0; padding: 2rem 1rem; }
              .box { max-width: 32rem; margin: 0 auto; background: #fff; border-radius: .75rem;
                     padding: 1.5rem; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
              h1 { font-size: 1.25rem; margin: 0 0 .75rem; }
              p { line-height: 1.5; margin: 0 0 .75rem; color: #555; }
              a { color: #1e7fd0; }
            </style></head>
            <body><div class="box">
              <h1>File not found</h1>
              <p>This document is still listed, but its file is no longer in storage. It may have been
                 removed, or lost when the server's storage was moved.</p>
              <p>Upload the file again from its lease or notice page. If you think this is a mistake,
                 contact your property manager.</p>
              <p><a href="javascript:history.back()">Go back</a></p>
            </div></body></html>
            """,
    };
}
