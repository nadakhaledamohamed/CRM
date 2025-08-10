using System;
using System.Collections.Generic;

namespace CRM.Models;

public class User_org
{
    public int tabelid { get; set; }

    public int userid { get; set; }

    public int Roleid { get; set; }

    public int Org_id { get; set; }

    public virtual Lookup_Organization Org { get; set; } = null!;

    public virtual LookupRole Role { get; set; } = null!;

    public virtual User user { get; set; } = null!;
}
