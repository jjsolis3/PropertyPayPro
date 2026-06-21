namespace PropertyPayPro.Pages.Shared;

public class KpiCardModel
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? SubText { get; set; }
    public string Color { get; set; } = "primary";
    public string Icon { get; set; } = "flaticon-381-list";
    public string? LinkPage { get; set; }
    public string? LinkText { get; set; }
}
