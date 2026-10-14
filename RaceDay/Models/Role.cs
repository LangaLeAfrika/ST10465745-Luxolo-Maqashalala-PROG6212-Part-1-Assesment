using System.Collections.Generic;

namespace RaceDay.Models;

public class Role
{
    public int RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public ICollection<AppUser> Users { get; set; }
        = new List<AppUser>();
}