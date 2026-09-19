// Every test class gets its own CustomWebApplicationFactory, which starts its own SQL Server and
// Redis containers and its own host. In parallel that means a dozen SQL Server instances on one CI
// runner (connections drop once it runs out of memory) and several hosts racing to freeze Serilog's
// static bootstrap logger ("The logger is already frozen"). Run the classes one at a time instead.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
