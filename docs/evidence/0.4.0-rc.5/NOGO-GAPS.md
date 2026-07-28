# Remaining No-Go Evidence Gaps after rc.5 seal

Sealed suite green is necessary but not sufficient for final 0.4.0.

| Blocker | Current state | Gap |
|---|---|---|
| Prompt Authority No-Go matrix | Covered by tests-next / canaries for many paths | Need explicit host-level matrix dump in final evidence if required by release guide |
| Provider-visible same-run A/A/B/B | Mock/plugin decision path covered (`fallback-canary`) | **Missing direct provider request trace** proving `A→A→B→B` and no 5th request |
| Omit-model inherits BaseModel not Side B | Covered in unit/host tests | Keep as part of AABB final package |
| Companion prefix byte-stability | Covered by companion cache/replacement canaries | Final evidence should keep digest/log artifacts if re-run |
| Review confirmation physical + ProviderRunId | Canaries + journal facts present | Final package should retain witness chain export |
| Inspector/Executor/PTY bounds | Canaries green | Final dispose/leak logs optional extras |
| Orchestrator crash recovery | restart-publish canary green | Keep as final evidence re-export |
| Dispose leaks | ProcessHost leak gate + canary dispose | Final cleanup checklist file still recommended |
| ReviewConfirmation accepted as HumanRoot | **Fixed in rc.6**: part-metadata + pending-claim correlation; live orch dump shows reviewer roots as AgentOwnerRoot only, confirmations as PluginPromptClaimed ReviewConfirmation | Keep regression tests green |
| Provider-visible same-run A/A/B/B **requests** | Durable `resolveForSession` A→A→B→B proven; PluginFallbackRetry claims EffectiveModel; host may not re-call provider after non-retryable APIError | Still final No-Go until wire-level request models A→A→B→B are observed |

## Default policy already locked

- Private delivery
- A/A/B/B blocking for final
- Scope freeze active
