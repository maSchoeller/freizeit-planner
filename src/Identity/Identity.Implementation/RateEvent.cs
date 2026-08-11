namespace Identity.Implementation;

public sealed record RateEvent(string Partition, DateTimeOffset OccurredAt);
