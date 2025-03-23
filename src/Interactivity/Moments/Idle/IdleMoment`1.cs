namespace OoLunar.DocBot.Interactivity.Moments.Idle
{
    public abstract record IdleMoment<T> : IdleMoment
    {
        public required T ComponentCreator { get; init; }
    }
}
