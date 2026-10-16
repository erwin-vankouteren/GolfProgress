namespace GolfProgress.Domain.Enums;

/// <summary>
/// What a user wants to improve. Stored by name in User.GoalFocus; micro-wins tie
/// their focus-area win to the first one.
/// </summary>
public enum GoalFocusArea { Putting, Penalties, TeeShots, Consistency }
