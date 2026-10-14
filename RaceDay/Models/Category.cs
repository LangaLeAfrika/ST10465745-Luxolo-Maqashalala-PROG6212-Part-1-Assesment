using System.Collections.Generic;

namespace RaceDay.Models;

public class Category
{
    public int CategoryId { get; set; }

    public int EventId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public int? MinimumAge { get; set; }

    public int? MaximumAge { get; set; }

    public Event Event { get; set; } = null!;

    public ICollection<Enrolment> Enrolments { get; set; }
        = new List<Enrolment>();
}