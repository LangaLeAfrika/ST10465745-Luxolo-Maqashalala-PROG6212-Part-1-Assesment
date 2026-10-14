using System;
using System.Collections.Generic;

namespace RaceDay.Models;

public class Event
{
    public int EventId { get; set; }

    public string EventName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly EventDate { get; set; }

    public string Location { get; set; } = string.Empty;

    public decimal Distance { get; set; }

    public string EventType { get; set; } = string.Empty;

    public int OrganiserId { get; set; }

    public AppUser Organiser { get; set; } = null!;

    public ICollection<Category> Categories { get; set; }
        = new List<Category>();

    public ICollection<Enrolment> Enrolments { get; set; }
        = new List<Enrolment>();
}