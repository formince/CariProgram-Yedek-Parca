namespace CariErinc.ViewModels;

public class PaginationInfo
{
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public string Action { get; set; } = "Index";
    public Dictionary<string, string> RouteValues { get; set; } = new();
}
