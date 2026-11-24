namespace SLA.Core
{
    /// <summary>
    /// Every screen in the GDD's screen flow (Technical architecture → Game states).
    /// <see cref="Tutorial"/> is added after the GDD list: the training room that stands in for the Prologue.
    /// </summary>
    public enum GameState
    {
        Boot,
        FirstRunSetup,
        Title,
        Hub,
        DailyQuest,
        PenaltyZone,
        Gate,
        Results,
        Paused,
        Tutorial,
    }
}
