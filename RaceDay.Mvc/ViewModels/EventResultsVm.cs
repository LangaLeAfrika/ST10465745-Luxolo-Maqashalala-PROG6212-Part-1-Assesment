using RaceDay.Contracts;

namespace RaceDay.Mvc.ViewModels;

public class EventResultsVm
{
    public EventDto Event { get; set; } = null!;

    public List<ResultDto> Results { get; set; } = new();
}