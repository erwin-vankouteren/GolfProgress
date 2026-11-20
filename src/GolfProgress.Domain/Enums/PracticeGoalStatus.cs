namespace GolfProgress.Domain.Enums;

/// <summary>Where a practice goal stands (#226). A goal past its end without either stays Active, and reads as ended.</summary>
public enum PracticeGoalStatus
{
    Active,
    Done,
    Dismissed,
}
