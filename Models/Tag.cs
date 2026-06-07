using System;
using System.Collections.Generic;

namespace AIStudyHub.Api.Models;

public partial class Tag
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
}
