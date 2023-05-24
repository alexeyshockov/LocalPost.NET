namespace LocalPost;

// For the DI container and, to distinguish between different queues
public sealed record BackgroundQueueOptions<T> : BackgroundQueueOptions;
