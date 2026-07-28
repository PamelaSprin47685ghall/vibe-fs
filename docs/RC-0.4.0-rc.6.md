# 0.4.0-rc.6 — release candidate

| Field | Value |
|---|---|
| Version | `0.4.0-rc.6` |
| Base | sealed rc.5 + production authority/fallback Host contract fixes |
| Date | 2026-07-28 |
| Status | Release candidate. **Not** final `0.4.0`. |
| Package | `private: true`, license `SEE LICENSE IN LICENSE` |
| Scope freeze | `docs/SCOPE-0.4.0-FREEZE.md` |

## Why a new RC

rc.5 sealed green, but journal evidence showed ReviewConfirmation / child prompts
accepted as `HumanRoot` because Host drops top-level `PromptInput.metadata`.
That is a production authority bug; production code changed → **rc.6**.

## Changes since rc.5

### Prompt Authority (production)

- Put `wanxiangshu_prompt_key` / origin on **text part metadata** (host-stable)
- `chat.message` recovers correlation from part metadata or a unique pending claim
- Reviewer confirmation no longer creates a new HumanRoot Logical Run
- Child fork prompts accept as `AgentOwnerRoot` when claim metadata is stripped

### Fallback / Host signals (production)

- Non-retryable `session.error` (no assistant message) drives PluginFallbackRetry
- Dual-source signal subscription: local listen + filtered global `session.error`
- ProviderError dual-delivery dedupe
- Optional `WANXIANGSHU_CHAT_MAX_RETRIES` → `experimental.chatMaxRetries`
- `chat.params` EffectiveModel injection hook (for same-run attempt selection)

### Tests / evidence

- New PromptAuthority chat.message correlation tests
- Fallback model-selection integration: resolveForSession A→A→B→B before Dead
- HostSignalAdapter tests for non-retryable session.error

## Still blocking final 0.4.0

- RC observation period (event-driven; no open P0/P1)
- **Blocking**: provider-visible same-run A/A/B/B **request** trajectory under host
  re-prompt after failures. Durable decision path is proven; host may still skip
  provider calls after non-retryable APIError until HostContract improves.
- Second clean-checkout gate on real version `0.4.0`
- Private delivery only unless a separate license decision is made

## Gate

Sealed on clean `git clean -xfd` + `npm ci` + `test:release` (`CANARY_REPEAT=3`) + pack + empty-dir install.
Evidence: `docs/evidence/0.4.0-rc.6/`.

| Gate | Result |
|---|---|
| clean `npm ci` | pass |
| `tests-next` | 281 passed |
| manager-tools + gate-testkit | pass (29) |
| `CANARY_REPEAT=3` 17 canaries | pass |
| pack + empty-dir install/import | pass |
