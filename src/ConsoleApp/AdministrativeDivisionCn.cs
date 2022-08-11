namespace ConsoleApp;

class AdministrativeDivisionCn
{
    public string Code { get; set; }
    public int Level { get; set; }
    public string Display { get; set; }
    public string ChildrenUrl { get; set; }
    public bool? ChildrenLoaded { get; set; } = null;

    public override string ToString() => $"【{Level}】{Code}=>{Display}{(ChildrenLoaded.HasValue ? $"\t\tChildrenLoaded:{ChildrenLoaded}" : null)}";
}
