# Changelog

## 1.4.0 (unreleased)

### BREAKING
- Error messages standardized across SDKs; `GetVariableKeys` now returns a list of experiment names per key.
- `GetContextDataAsync` failures now throw (the real load error is kept); new not-ready context state.
- `ContextEventHandler` renamed to `ContextPublisher` (obsolete aliases and legacy `ABSdk`/DI factory contracts kept).

### Added
- `IContextV2 : IContext`, implemented by `Context`, carries every new context member; `IContext` is unchanged from 1.3.0 so existing implementations keep compiling. `CreateContext*` still return `IContext` (cast to `IContextV2`); `CreatePendingContext` returns `IContextV2`.
- `FinalizeAsync()` on `IContextV2`/`Context`: publishes and seals the context; units, attributes and experiments stay readable afterwards. `Close()` no longer releases the context's locks (only `Dispose`/`DisposeAsync` do).
- `Md5.Utf8Bytes` and `Md5.Base64UrlNoPadding` hashing helpers.
- `Finalize` aliases, `GetCustomFieldValueType`, `GetUnits`, `GetAttribute`, `GetAttributes`, `ReadyError` (on `IContextV2`/`Context`).
- Package validation against the published 1.3.0 package (`EnablePackageValidation`), so API breaks fail the build.

### Fixed
- `CreateContext`/`CreateContextAsync` return only after the `Ready`/`Error` event has been logged.
- JSON expr: haystack-first `in` (CONTAINS semantics); `eq(null, null)` returns null.
- `Context.Flush` serialized to prevent concurrent-flush races; astral/multibyte unit hashing.
- Publish failures rethrow the original exception without a logger factory.
