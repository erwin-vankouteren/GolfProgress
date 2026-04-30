namespace GolfProgress.Api.Contracts.Reflections
{
    public sealed class ReflectionResponse
    {
        public Guid ReflectionId { get; init; }
        public Guid UserId { get; init; }
        public Guid? RoundId { get; init; }

        public short? FocusRating { get; init; }
        public short? MoodRating { get; init; }
        public bool? FrustrationSpillover { get; init; }

        public string? WhatWorked { get; init; }
        public string? WhatToRepeat { get; init; }
        public string? OneChangeNext { get; init; }

        public DateTimeOffset CreatedAt { get; init; }
    }

}
