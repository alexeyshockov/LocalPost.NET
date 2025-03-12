namespace LocalPost;

// int, 1 <= value <= int.MaxValue
internal readonly record struct PositiveInt
{
    public static implicit operator int(PositiveInt num) => num.Value;

    public static implicit operator PositiveInt(int num) => new(num);
    public static implicit operator PositiveInt(short num) => new(num);
    public static implicit operator PositiveInt(ushort num) => new(num);

    private readonly int _value;
    public int Value => _value == 0 ? 1 : _value; // Default value

    private PositiveInt(int num)
    {
        if (num < 1)
            throw new ArgumentOutOfRangeException(nameof(num), num, "Must be greater than or equal to 1");

        _value = num;
    }

    public void Deconstruct(out int value)
    {
        value = Value;
    }
}
