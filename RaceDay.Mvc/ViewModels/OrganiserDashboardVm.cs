using RaceDay.Contracts;

namespace RaceDay.Mvc.ViewModels;

public class OrganiserDashboardVm
{
    public List<EventDto> Events { get; set; } = new();

    public DateOnly Today { get; set; }

    public Dictionary<int, int> EnrolmentCounts { get; set; } = new();

    public int TotalEvents => Events.Count;

    public int UpcomingEvents =>
        Events.Count(e => e.EventDate >= Today);

    public int TotalEnrolments => EnrolmentCounts.Values.Sum();
}