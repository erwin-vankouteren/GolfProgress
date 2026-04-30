namespace GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;


public class Reflection
{
    public Guid ReflectionId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public Guid? RoundId { get; set; }
    public Round? Round { get; set; }

    public short? FocusRating { get; set; }   // 1..5
    public short? MoodRating { get; set; }    // 1..5
    public bool? FrustrationSpillover { get; set; }

    public string? WhatWorked { get; set; }
    public string? WhatToRepeat { get; set; }
    public string? OneChangeNext { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
