namespace RaceDay.Mvc.ViewModels;

public class ImageUploadVm
{
    public int? EventId { get; set; }
    public string Title => EventId.HasValue ? "Event banner" : "Profile photo";
    public IFormFile? File { get; set; }
}
