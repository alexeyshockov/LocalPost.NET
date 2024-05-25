namespace LocalPost;

// [PublicAPI]
internal readonly record struct MaxSize
{
    public static implicit operator int(MaxSize batchSize) => batchSize.Value;

    public static implicit operator MaxSize(int batchSize) => new(batchSize);
    public static implicit operator MaxSize(short batchSize) => new(batchSize);
    public static implicit operator MaxSize(ushort batchSize) => new(batchSize);

    public readonly int Value = 1;

    public MaxSize(int value)
    {
        if (value <= 1)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Batch size must be positive.");

        Value = value;
    }
}
