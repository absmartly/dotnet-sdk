# Changelog

## 1.4.0 (unreleased)

There are no source or binary API breaks against 1.3.0; this is enforced at build time by package validation against the published 1.3.0 package. Public signatures that were reshaped during development are kept as `[Obsolete]` forwarders (`ABSdk`, `ABSdkConfig`, `ABSdkHttpClientFactory`, `IABSdkHttpClientFactory`, the `ABSmartlyService` and `Context` constructors taking the legacy types, `IContextEventHandler`).

### Added
- `IContextV2 : IContext`, implemented by `Context`, carries every new context member (`ReadyError`, `WaitUntilReady()`/`WaitUntilReadyAsync()`, `IsFinalized`, `IsFinalizing`, `Experiments`, `Units`, `Attributes`, `GetAttribute()`, `VariableKeys`, `Close()`, `FinalizeAsync()`). `IContext` is unchanged from 1.3.0, so existing implementations keep compiling. `CreateContext*` still return `IContext` (cast to `IContextV2`); `CreatePendingContext()` returns `IContextV2` straight away, before the data has loaded.
- `FinalizeAsync()` and `Close()` on `IContextV2`/`Context`: publish pending events and seal the context; units, attributes and experiments stay readable afterwards. Only `Dispose`/`DisposeAsync` release the context's locks.
- `ABsmartly`, `ABsmartlyConfig`, `ABsmartlyHttpClientFactory` and `IABsmartlyHttpClientFactory` as the new names of `ABSdk`, `ABSdkConfig`, `ABSdkHttpClientFactory` and `IABSdkHttpClientFactory`; `IContextPublisher`/`ContextPublisher` as the new name of `IContextEventHandler`/`ContextEventHandler`. `AddABsmartly()` as the new name of the `AddABSmartly()` DI extension. The old names still work and are marked `[Obsolete]`.
- `GetCustomFieldValueType`, `GetUnits`, `GetAttributes` and `GetAttribute` on `IContextV2`/`Context`.
- `Md5.Utf8Bytes` and `Md5.Base64UrlNoPadding` hashing helpers.
- Package validation against the published 1.3.0 package (`EnablePackageValidation`), so API breaks fail the build.

### Changed
- A failed initial load yields a ready-but-failed context (`IsFailed()`, `ReadyError`, an `Error` event); `CreateContext*` no longer throw. A failed context returns control variants and default variable values, and `Refresh` on it is a no-op.
- `GetContextDataAsync` throws the real load error (an `InvalidOperationException` for an empty or malformed body) instead of returning `null`.
- A failed publish or close keeps its events and can be retried, when the publisher reports the failure by throwing or returning a faulted task. The default publisher does not report an unsuccessful HTTP response this way (unchanged from 1.3.0), so those events are not retried; supply an `IContextPublisher` that throws on failure to retry them. `FinalizeAsync`/`DisposeAsync` (and `Close()`/`Dispose()` on threads without a `SynchronizationContext`) wait for publications already in flight, and a flush that starts during a close leaves its events to the close. A publish that fails during shutdown has its events retried by the close instead of being stranded on a finalized context. As a result, a slow or hung publisher now delays the close. A synchronous `Close()`/`Dispose()` on a UI or classic ASP.NET thread, or a publisher closing its own context, does not wait for outstanding publications or for a close already in progress, because waiting there could deadlock. The context is not sealed while such publications are outstanding: the close finishes in the background once they settle, retrying any events they fail to deliver, and `Dispose` releases the locks once it completes. Use `FinalizeAsync`/`DisposeAsync` on such threads to wait for the whole close. A publisher must not block waiting for a close of its own context that runs on another thread.
- A failed close leaves the context open, without re-arming the refresh timer.
- `http://` endpoints are still accepted, as in 1.3.0 and the Java SDK, but now log a plaintext warning. Endpoints with any other scheme now throw `ArgumentException` (1.3.0 did not check the scheme). Audience `match` regexes keep 1.3.0/Java behaviour, with no match timeout. Both are deliberate owner decisions; see "Security considerations" in the README.
- Non-integer `number` custom fields parse as `double` (integers still parse as `long`).
- `System.Text.Json` `JsonElement` attributes now work in audience expressions.
- `SetUnit` with the UID already set is a no-op; a different UID throws `ArgumentException` with the message `Unit '<type>' UID already set.`, as the Java SDK.
- `Context.Dispose(bool)` is `public` again, as in 1.3.0.

### Fixed
- Number custom fields parse with the invariant culture (`"1.5"` was read as `15` on de-DE, pt-BR, fr-FR, ...).
- `json` custom fields use the configured `IVariableParser` again.
- `GetAttribute()`/`Attributes` take the context read lock, so they can no longer throw while `SetAttribute` runs concurrently.
- The audience is no longer re-evaluated on every `GetTreatment`/`GetVariableValue` after a `SetAttribute` once the assignment has been revalidated.
- `GetVariableValue` falls through to the next experiment when the selected variant of an experiment does not define the key.
- A publisher that calls `PublishAsync()`/`FinalizeAsync()` on its context no longer deadlocks: the batch is detached before the publisher runs and no lock is held across it.
- A `Close()`/`Dispose()` from another thread no longer deadlocks with an event logger that waits on it: the close attempt settles before the Publish/Error/Close observers run.
- Exceptions thrown by an event logger no longer turn a delivered publish into a failure that resends the batch, or mask the publish error (they still propagate for other events).
- As in 1.3.0, an event logger that throws on the `Ready` or `Error` event of context creation makes `CreateContext`/`CreateContextAsync` throw that exception. `CreatePendingContext` readiness does not depend on it.
- Trailing slash on the endpoint is trimmed.
- Concurrent `Flush` calls publish disjoint batches (no duplicate or lost events), and `PendingCount` is 0 while a publish is in flight and restored on failure.
- `CreateContext`/`CreateContextAsync` return only after the `Ready`/`Error` event has been logged.
- Custom-field reads work after the context is closed.
- Publish failures rethrow the original exception when no logger factory is configured.
