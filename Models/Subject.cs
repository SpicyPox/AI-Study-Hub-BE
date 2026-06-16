using System;
using System.Collections.Generic;

namespace AIStudyHub.Api.Models;

public partial class Subject
{
    public Guid Id { get; set; }

    public string? Code { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
}
