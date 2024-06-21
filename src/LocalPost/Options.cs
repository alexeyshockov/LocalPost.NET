namespace LocalPost;

public sealed record BatchOptions(int MaxSize = 10, int TimeWindowDuration = 1_000);
