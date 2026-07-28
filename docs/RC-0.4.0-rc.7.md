# 0.4.0-rc.7 — release candidate

| Field | Value |
|---|---|
| Version | `0.4.0-rc.7` |
| Base | sealed rc.6 + provider-visible same-run A/A/B/B wire path |
| Date | 2026-07-28 |
| Status | Release candidate. **Not** final `0.4.0`. |
| Package | `private: true` |
| Scope freeze | `docs/SCOPE-0.4.0-FREEZE.md` |

## Why a new RC

rc.6 sealed the authority correlation fix, but final No-Go still required
**provider-visible** same-run `A → A → B → B` request evidence. That path needed
production changes:

1. Host `prompt_async` after `session.error` only starts a provider loop once the
   runner is fully idle (immediate / early continue creates a user message with
   no request).
2. Plugin records durable `FallbackFailure` on non-retryable `session.error`, then
   **debounces** `ProviderRetryAttempt` across multi-idle teardown ticks.
3. EffectiveModel is selected via `resolveForSession` so attempts 3–4 use Side B.

## Changes since rc.6

- Debounced `PluginFallbackRetry.scheduleFlushOnIdle` (250ms settle)
- `HostSignalBootstrapTimers` for Node-safe setTimeout/clearTimeout
- Mock prefix reseal on fallback model-side cold boundary (system embeds model id)
- Canary: `fallback-aabb-trace` (18th scenario) proves wire models A/A/B/B

## Still blocking final 0.4.0

- RC observation period (event-driven)
- Final cut to version `0.4.0` + second clean-checkout gate
- Private delivery default

## Gate

Requires clean `npm ci` + `test:release` (`CANARY_REPEAT=3`) including the new
AABB canary, plus pack + empty-dir install. Evidence: `docs/evidence/0.4.0-rc.7/`.
