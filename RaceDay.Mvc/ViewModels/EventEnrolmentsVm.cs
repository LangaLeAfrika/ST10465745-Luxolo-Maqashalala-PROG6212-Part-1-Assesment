using RaceDay.Contracts;

namespace RaceDay.Mvc.ViewModels;

public class EventEnrolmentsVm
{
    public EventDto Event { get; set; } = null!;

    public List<EnrolmentDto> Enrolments { get; set; } = new();

    public int TotalEntries => Enrolments.Count;
}