# Real-World Incrementalist Examples

This page contains some links to public repositories where Incrementalist is being used successfully in the wild.

## Akka.NET

The [Akka.NET project](https://github.com/akkadotnet/akka.net) was the original use case that inspired the creation of Incrementalist:

### Example: "Run Only .NET Framework Tests"

* `incrementalist` call: https://github.com/akkadotnet/akka.net/blob/c6a3068c137591039fbcc42f30332e4bc6d75d13/build-system/pr-validation.yaml#L62
* configuration file: https://github.com/akkadotnet/akka.net/blob/c6a3068c137591039fbcc42f30332e4bc6d75d13/.incrementalist/testsOnly.json#L1-L18

### Example: "Run Only Multi-Node Tests"

* `incrementalist` call: https://github.com/akkadotnet/akka.net/blob/c6a3068c137591039fbcc42f30332e4bc6d75d13/build-system/pr-validation.yaml#L101
* configuration file: https://github.com/akkadotnet/akka.net/blob/c6a3068c137591039fbcc42f30332e4bc6d75d13/.incrementalist/mutliNodeOnly.json#L1-L13

## Akka.Management

[Akka.Management](https://github.com/akkadotnet/Akka.Management) is another large project in the Akka.NET ecosystem, used for performing cluster service discovery and adding support for distributed locks backed by specific cloud platforms such as AWS, Azure, and Kubernetes.

### Example: "Skip `TestContainer` Projects on Windows Build Agents"

This file is used to avoid executing test projects that rely on [Azurite TestContainer support](https://testcontainers.com/modules/azurite/), since Windows build agents can't run those Docker images.

* `incrementalist` call: https://github.com/akkadotnet/Akka.Management/blob/ec6234341c59bb1760a807685021931b6a2469d1/build-system/azure-pipeline.template.yaml#L38-L40
* configuration file: https://github.com/akkadotnet/Akka.Management/blob/ec6234341c59bb1760a807685021931b6a2469d1/.incrementalist/windowsDevOpsBuilds.json#L1-L14

### Example: "Run All Tests"

Akka.Management uses this configuration on Linux agents, where the full test suite can be run without platform compatibility issues.

* `incrementalist` call: https://github.com/akkadotnet/Akka.Management/blob/ec6234341c59bb1760a807685021931b6a2469d1/build-system/azure-pipeline.template.yaml#L38-L40
* configuration file: https://github.com/akkadotnet/Akka.Management/blob/ec6234341c59bb1760a807685021931b6a2469d1/.incrementalist/incrementalist.json#L1-L12 - essentially a default configuration file.

## Using the `run-process` Verb

The `run-process` verb is a discoverable alias for the `run` verb that currently only supports dotnet commands. It was introduced to improve CLI discoverability for future support of non-dotnet commands.

### Example: "Run dotnet test with run-process"

```shell
# This is equivalent to: incrementalist run -b dev -- test -c Release
incrementalist run-process --process dotnet -b dev -- test -c Release
```

Currently, the `--process` parameter must be set to `dotnet`, but the verb provides a more discoverable interface for future enhancements.