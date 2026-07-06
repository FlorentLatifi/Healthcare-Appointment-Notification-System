using System.Collections.Concurrent;
using FluentAssertions;
using Healthcare.Adapters.Services;
using Healthcare.Domain.Services;
using Moq;
using StackExchange.Redis;

namespace Healthcare.UnitTests.Adapters.Services;

public sealed class RedisAppointmentCodeGeneratorTests
{
    [Fact]
    public async Task GenerateCode_TwoInstancesConcurrently_ProducesNoDuplicates()
    {
        var sharedCounter = 0;
        var dateString = DateTime.UtcNow.ToString("yyyyMMdd");
        var counterKey = $"appt-code-counter:{dateString}";

        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.StringIncrement(counterKey, It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .Returns(() => Interlocked.Increment(ref sharedCounter));
        dbMock.Setup(d => d.KeyExpire(counterKey, It.IsAny<TimeSpan>(), It.IsAny<CommandFlags>()))
            .Returns(true);

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(dbMock.Object);

        var generator1 = new RedisAppointmentCodeGenerator(multiplexerMock.Object);
        var generator2 = new RedisAppointmentCodeGenerator(multiplexerMock.Object);

        int count = 100;
        var codes = new ConcurrentBag<string>();

        var task1 = Task.Run(() =>
        {
            for (int i = 0; i < count; i++)
                codes.Add(generator1.GenerateCode());
        });

        var task2 = Task.Run(() =>
        {
            for (int i = 0; i < count; i++)
                codes.Add(generator2.GenerateCode());
        });

        await Task.WhenAll(task1, task2);

        codes.Should().HaveCount(count * 2);
        codes.Distinct().Should().HaveCount(count * 2);
        codes.All(c => c.StartsWith($"APT-{dateString}-")).Should().BeTrue();
    }

    [Fact]
    public void GenerateCode_ShouldProduceSequentialCodes()
    {
        var sharedCounter = 0;
        var dateString = DateTime.UtcNow.ToString("yyyyMMdd");
        var counterKey = $"appt-code-counter:{dateString}";

        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.StringIncrement(counterKey, It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .Returns(() => Interlocked.Increment(ref sharedCounter));
        dbMock.Setup(d => d.KeyExpire(counterKey, It.IsAny<TimeSpan>(), It.IsAny<CommandFlags>()))
            .Returns(true);

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(dbMock.Object);

        var generator = new RedisAppointmentCodeGenerator(multiplexerMock.Object);

        var code1 = generator.GenerateCode();
        var code2 = generator.GenerateCode();
        var code3 = generator.GenerateCode();

        code1.Should().Be($"APT-{dateString}-0001");
        code2.Should().Be($"APT-{dateString}-0002");
        code3.Should().Be($"APT-{dateString}-0003");
    }

    [Fact]
    public void GenerateCode_Implements_IAppointmentCodeGenerator()
    {
        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        var generator = new RedisAppointmentCodeGenerator(multiplexerMock.Object);
        generator.Should().BeAssignableTo<IAppointmentCodeGenerator>();
    }
}
