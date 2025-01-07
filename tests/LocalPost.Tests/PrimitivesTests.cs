namespace LocalPost.Tests;

public class PrimitivesTests
{
    [Fact]
    public void MaxSize_implicit_conversion()
    {
        PositiveInt batchSize = default;
        int value = batchSize;
        value.Should().Be(1);

        batchSize = 1;
        value = batchSize;
        value.Should().Be(1);

        batchSize = 2;
        value = batchSize;
        value.Should().Be(2);

        batchSize = (short)3;
        value = batchSize;
        value.Should().Be(3);

        batchSize = (ushort)4;
        value = batchSize;
        value.Should().Be(4);
    }
}
