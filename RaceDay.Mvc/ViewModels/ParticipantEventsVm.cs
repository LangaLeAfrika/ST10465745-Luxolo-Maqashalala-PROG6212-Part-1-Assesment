using RaceDay.Contracts;

namespace RaceDay.Mvc.ViewModels;

public class ParticipantEventsVm
{
    public List<EventDto> Events { get; set; } = new();

    public string? Search { get; set; }

    public string? EventType { get; set; }

    public string? Location { get; set; }
}