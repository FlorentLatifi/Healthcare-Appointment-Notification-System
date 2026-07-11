# Architecture tests moved

Layer dependency rules are enforced by the dedicated project:

`Healthcare.ArchitectureTests/ArchitectureTests.cs`

That project runs as a separate CI job (`architecture-tests`) and fails the build on violations.
