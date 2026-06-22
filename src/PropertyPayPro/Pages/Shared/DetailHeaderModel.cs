namespace PropertyPayPro.Pages.Shared;

public class DetailHeaderModel
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? IconClass { get; set; }          // e.g. "flaticon-381-user-9"
    public string IconColor { get; set; } = "primary";
    public string? Initials { get; set; }           // shown if IconClass is null
    public List<DetailHeaderBadge> Badges { get; set; } = new();
}

public class DetailHeaderBadge
{
    public string Text { get; set; } = string.Empty;
    public string Color { get; set; } = "secondary";  // bg color suffix
    public string? Title { get; set; }                // tooltip
}
