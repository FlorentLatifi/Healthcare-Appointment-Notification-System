using Xunit;

namespace Healthcare.UnitTests.Presentation;

[CollectionDefinition("RateLimitingSequential", DisableParallelization = true)]
public sealed class RateLimitingTestCollection { }
