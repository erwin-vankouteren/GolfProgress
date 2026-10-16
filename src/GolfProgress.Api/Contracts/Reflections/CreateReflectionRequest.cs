using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Contracts.Reflections
{
    /// <summary>
    /// A reflection on a round. Every field is optional, but at least one must be given
    /// (#99): an empty reflection says nothing, yet would still count towards Habits.
    /// </summary>
    public sealed class CreateReflectionRequest : IValidatableObject
    {
        /// <summary>Longest text a reflection field takes.</summary>
        public const int TextMaxLength = 1000;

        [Range(1, 5)] public short? FocusRating { get; init; }
        [Range(1, 5)] public short? MoodRating { get; init; }
        public bool? FrustrationSpillover { get; init; }

        [MaxLength(TextMaxLength)] public string? WhatWorked { get; init; }
        [MaxLength(TextMaxLength)] public string? WhatToRepeat { get; init; }
        [MaxLength(TextMaxLength)] public string? OneChangeNext { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var anything =
                FocusRating.HasValue || MoodRating.HasValue || FrustrationSpillover.HasValue ||
                !string.IsNullOrWhiteSpace(WhatWorked) ||
                !string.IsNullOrWhiteSpace(WhatToRepeat) ||
                !string.IsNullOrWhiteSpace(OneChangeNext);

            if (!anything)
                yield return new ValidationResult("Fill in at least one field.", new[] { "Reflection" });
        }
    }

}
