namespace GolfProgress.Api.Contracts.Reflections
{
    public sealed class CreateReflectionRequest
    {
        public short? FocusRating { get; init; }            // 1..5
        public short? MoodRating { get; init; }             // 1..5
        public bool? FrustrationSpillover { get; init; }

        public string? WhatWorked { get; init; }
        public string? WhatToRepeat { get; init; }
        public string? OneChangeNext { get; init; }
    }

}
