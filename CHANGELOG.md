# Changelog

## 1.4.0 (unreleased)

### BREAKING
- Error messages standardized across SDKs; `GetVariableKeys` now returns a list of experiment names per key.
- `GetContextDataAsync` failures now throw (the real load error is kept); new not-ready context state.
- `ContextEventHandler` renamed to `ContextPublisher` (obsolete aliases and legacy `ABSdk`/DI factory contracts kept).

### Added
- `Finalize` aliases, `GetCustomFieldValueType`, `GetUnits`, `GetAttribute`, `GetAttributes`, `ReadyError`.

### Fixed
- JSON expr: haystack-first `in` (CONTAINS semantics); `eq(null, null)` returns null.
- `Context.Flush` serialized to prevent concurrent-flush races; astral/multibyte unit hashing.
- Publish failures rethrow the original exception without a logger factory.
