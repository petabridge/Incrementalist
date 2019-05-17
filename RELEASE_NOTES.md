#### 0.1.4 May 17 2019 ####
Bugfix release for Incrementalist v0.1.3

Fixed [MSBuild graph issue where two unrelated changes in the same branch could overwrite each other in the Incrementalist output](https://github.com/petabridge/Incrementalist/issues/49).

In the event of three concurrent MSBuild dependency graphs like these:

[A modified] Project A --> B --> C
[B modified] Project B --> C
[D modified] Project D --> E

In Incrementalist 0.1.3, you'd only see this graph: `Project A --> B --> C` because it was the longest and "covered" all of the other graphs. In Incrementalist 0.1.4 you'll now see the following build output:

Project A --> B --> C
Project D --> E

Each line represents its own independent graph, uncovered by any of the other graphs detected in the topology of the MSBuild solution.