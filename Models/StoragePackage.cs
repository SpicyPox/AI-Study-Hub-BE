using System;
using System.Collections.Generic;

namespace AIStudyHub.Api.Models;

public partial class StoragePackage
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public long CapacityBytes { get; set; }

    public decimal Price { get; set; }

    public bool? IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
