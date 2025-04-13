# 🚀 Dependency Graph Caching

> ![WARNING]
> Caching is currently disabled due to https://github.com/petabridge/Incrementalist/issues/350 - we'll need to implement an alternative way of identifying projects in order to remedy this, as their MSBUILD `ProjectId`s are not stable between executions.

Incrementalist includes a caching system that significantly improves performance by storing and reusing project dependency information. This document explains how the cache works and provides best practices for using it effectively.

## How It Works

### Cache Structure
The cache is stored in `.incrementalist/incrementalist.graphcache.json` and contains:
- Version information to handle cache format changes
- Solution path relative to repository root
- A checksum of all project files to detect changes
- Project dependency information including:
  - Project IDs (for internal tracking)
  - Display paths (for human readability)
  - Dependencies between projects

### Cache Validation
The cache is considered valid when:
1. The cache version matches the current Incrementalist version
2. The solution path matches the current solution
3. The checksum of all project files matches the cached checksum

If any of these conditions fail, Incrementalist performs a full Roslyn analysis to rebuild the cache.

### Performance Benefits
- Avoids expensive Roslyn solution loading and analysis
- Reduces memory usage
- Significantly faster for repeated runs
- Particularly beneficial for large solutions and CI/CD pipelines

## Best Practices

### 1. Version Control Integration

```shell
git add .incrementalist/incrementalist.graphcache.json
git commit -m "Add Incrementalist cache"
```

**Recommendation**: Commit the cache to source control. Benefits include:
- Shared performance improvements across team members
- Cache "warms up" automatically on fresh clones
- Provides visibility into dependency changes over time

### 2. Cache Invalidation
The cache automatically invalidates when:
- Project files are modified
- Solution structure changes
- Incrementalist version changes

To force a cache rebuild, simply delete the cache file and run Incrementalist again:
```shell
rm .incrementalist/incrementalist.graphcache.json
incrementalist -b dev
```

### 3. Troubleshooting

If you experience unexpected behavior:

1. Check cache validity:
   ```shell
   incrementalist -b dev --verbose
   ```

2. Rebuild the cache:
   ```shell
   rm .incrementalist/incrementalist.graphcache.json
   incrementalist -b dev
   ```

3. Run without caching:
   ```shell
   incrementalist -b dev --no-cache
   ```
   The `--no-cache` option disables cache usage entirely, forcing a full Roslyn analysis. This can be helpful when debugging dependency issues.

4. Verify cache location:
   ```shell
   ls .incrementalist/incrementalist.graphcache.json
   ```

### 4. Cache File Management

- Keep the cache in version control unless you have specific reasons not to
- If you modify project dependencies, commit the updated cache
- Consider cache file in code reviews (it provides visibility into dependency changes)

## Technical Details

### Cache Format
```json
{
  "Version": "1.0",
  "SolutionPath": "MySolution.sln",
  "Checksum": "base64-encoded-hash",
  "Projects": {
    "project-id-guid": {
      "DisplayPath": "src/Project1/Project1.csproj",
      "Dependencies": [
        "dependent-project-id-guid"
      ]
    }
  }
}
```

### Checksum Calculation
The cache checksum includes:
- Solution file content and timestamp
- All project file contents and timestamps
- Sorted to ensure consistency

This provides reliable detection of relevant changes while ignoring unrelated modifications. 