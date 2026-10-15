using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Contracts.Reflections
{
    public sealed class CreateReflectionRequest
    {
        [Range(1, 5)] public short? FocusRating { get; init; }
        [Range(1, 5)] public short? MoodRating { get; init; }
        public bool? FrustrationSpillover { get; init; }

        public string? WhatWorked { get; init; }
        public string? WhatToRepeat { get; init; }
        public string? OneChangeNext { get; init; }
    }

}
