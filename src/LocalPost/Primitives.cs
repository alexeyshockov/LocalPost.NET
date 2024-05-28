namespace LocalPost;

// int, 1 <= value <= int.MaxValue
internal readonly record struct MaxSize
{
    public static implicit operator int(MaxSize batchSize) => batchSize.Value;

    public static implicit operator MaxSize(int batchSize) => new(batchSize);
    public static implicit operator MaxSize(short batchSize) => new(batchSize);
    public static implicit operator MaxSize(ushort batchSize) => new(batchSize);

    private readonly int _value;
    public int Value => _value == 0 ? 1 : _value; // Default value...

    public MaxSize(int value)
    {
        if (value < 1)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be greater than or equal to 1");

        _value = value;
    }
}
