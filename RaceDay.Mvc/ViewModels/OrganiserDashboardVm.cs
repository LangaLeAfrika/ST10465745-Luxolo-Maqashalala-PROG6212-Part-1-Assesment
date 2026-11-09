using RaceDay.Contracts;

namespace RaceDay.Mvc.ViewModels;

public class OrganiserDashboardVm
{
    public List<EventDto> Events { get; set; } = new();

    public DateOnly Today { get; set; }

    public int TotalEvents => Events.Count;

    public int UpcomingEvents =>
        Events.Count(e => e.EventDate >= Today);
}