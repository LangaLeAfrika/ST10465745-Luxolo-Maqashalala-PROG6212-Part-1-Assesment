using RaceDay.Contracts;

namespace RaceDay.Mvc.ViewModels;

public class EventCategoriesVm
{
    public EventDto Event { get; set; } = null!;

    public List<CategoryDto> Categories { get; set; } = new();

    public CreateCategoryRequest NewCategory { get; set; } = new();
}