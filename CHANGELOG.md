# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0] - 2026-03-14

### Added

- `SceneDependencies.Block()`: blocks activation of the next dependency group during a load sequence.
   This allows dependency scenes to prevent their dependents from being loaded until they are fully ready when they are waiting for some multi-frame operation.

## [2.0.0] - 2026-03-12

### Changed

- BREAKING! `SceneDependencies` now returns a `Handle` object which must be used to release dependency scene references and unload dependency scenes.
  - Unused dependency scenes are no longer automatically unloaded when `LoadDependenciesAsync` is called

### Removed

- `STM_SceneDependencyManager.UnloadUnusedDependencies`

## [1.2.0] - 2026-02-11

### Changed

- BREAKING! `SceneDependencyList` is now an abstract base class for `RegexSceneDependencyList`
    - Use Inspector debug mode to update existing asset lists to use `RegexSceneDependencyList.cs`
- Added `ExplicitSceneDependencyList`

## [1.1.0] - 2026-02-10

### Changed

- Load and fully activate dependency group before loading next group as a quick fix for an issue causing dependencies to get stuck loading.

## [1.0.1] - 2025-12-22

### Fixed

- Fix dependencies to load not calculated correctly when domain reload is disabled
- Make sure we fully supoprt disabled domain reloading

## [1.0.0] - 2025-11-25

- Initial release!
