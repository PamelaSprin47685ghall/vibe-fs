# 0.4.0 Scope Freeze

| Field | Value |
|---|---|
| Status | **Frozen** for final `0.4.0` track |
| Ship marker base | `0.4.0-rc.5` |
| Freeze date | 2026-07-28 |
| Distribution default | **Private** (`private: true`, provisional commercial `LICENSE`) |

This document is the 0.4.0 feature freeze line. Anything not listed as in-scope is out of 0.4.0 unless reclassified with an explicit freeze amendment.

## In scope (already in rc.5 code cut)

- Structured Agent Program (Flow CE, no Stage/Phase/Lease platform)
- Prompt Authority / Logical Run / synthetic continuation rules
- Companion Blogger + ActivePrefixEpoch / FrozenB cache protection
- Manager `fork / join / list`; Orchestrator `fork / join`
- Static role matrix (Orchestrator, Manager, Coder, Inspector, DevOps, Browser, Meditator, Reviewer, Executor Agent, Blogger)
- Full role system prompts (`next/prompts/*.md`)
- Session-wide formal assistant A accumulation; `join.finalText` = session-wide A
- Session-wide B = Companion LatestB work record
- Parent background: B preferred, else session-wide formal A
- Logical-Run Fallback A/A/B/B with durable `session.status=retry` writer
- ReviewGuard + dual PERFECT with physical confirmation + ProviderRunIdentity binding
- Historical 0.4.0 boundary: Inspector = `{executor}` only; 0.5.0 supersedes it with `{read, glob, grep, executor}`. One-shot Coder delegation and the private executor mailbox remain unchanged.
- Process/Executor: `3× estimate` deadline, large gate, 200KB ripple-carry summary
- PTY via DevOps `fork-pty` only; onExit-only completion; structured signals
- Orchestrator clean gate, worktree, serial publish lock, rebase, re-review, ff-only, crash facts
- OpenCode adapter: idle/retry/deleted signal + single-flight reconcile; official compaction off
- Package remains private under provisional commercial license

## Allowed code changes after freeze

- Blocking defect fixes
- Test / diagnostic strengthening that does **not** change product semantics
- Release script / packaging fixes
- Documentation and version sync
- Deterministic non-semantic correctness fixes required by gate failures

## Not allowed in 0.4.0

- New roles, tools, durable fact kinds, or Host protocol surfaces
- Prefetcher / Enforcer / Sidecar Supervisor (`future/FEATURE.md`)
- Public npm publish without a separate license decision
- Semantic changes to Authority, Fallback, Companion eligibility, dual PERFECT, PTY ownership, or Orchestrator publish

## Classification of open items

### 0.4.0 blocking (must close before final)

| Item | Why blocking |
|---|---|
| Clean-checkout seal of current RC (`npm ci` + `test:release` ×3 canaries + pack/install) | RC is not sealed without evidence |
| Provider-visible same-run A/A/B/B request trajectory | Final 0.4.0 No-Go without direct request evidence |
| Prompt Authority No-Go matrix evidence | Authority / continuation fail-closed is release critical |
| Companion prefix byte-stability evidence | Cache correctness is product-critical |
| Review confirmation full witness chain evidence | Dual PERFECT integrity |
| Inspector / Executor / PTY resource + permission bounds | Safety and role matrix |
| Orchestrator crash recovery + dispose cleanup evidence | Publish safety |
| Unified release policy text (no “optional” vs “No-Go” conflict) | Prevent false Go |
| Final `0.4.0` version cut + second clean gate + evidence | Promotion boundary |
| Explicit private delivery policy (no public npm) | License reality |

### 0.4.1 (post-final, non-blocking)

| Item | Note |
|---|---|
| Additional real-provider soak scenarios beyond freeze matrix | Observation follow-ups after final if needed |
| Packaging / install UX polish that does not change runtime semantics | |
| Extra diagnostics and canary coverage that only strengthen already-frozen rules | |
| Documentation polish after final evidence | |

### Future architecture (not 0.4.x feature track without re-freeze)

| Item | Source |
|---|---|
| Prefetcher sidecar | `future/FEATURE.md` |
| Enforcer sidecar | `future/FEATURE.md` |
| Sidecar Supervisor / durable prefetch overlay | `future/FEATURE.md` |
| New Host hooks beyond current OpenCode surface | AGENTS.md “do not modify OpenCode body” |
| Public registry publish under a new formal OSS/commercial license | Requires license decision outside freeze |

## Release policy decisions locked by this freeze

1. **A/A/B/B provider request traces are blocking** for final `0.4.0` (not optional observation).
2. **Default final distribution is private**: keep `private: true` and provisional commercial `LICENSE`; deliver tarball only under written commercial agreement. Public npm is out of scope until license changes.
3. **rc.5 (or a later rc if production code changes)** must be sealed with `docs/evidence/0.4.0-rc.x/` before observation/promotion.
4. Any production code change after an RC seal → cut **rc.N+1** and re-run the full clean gate. No “bundle at the end” change packs.

## Exit from freeze (when final is allowed to start)

- Scope freeze doc present and unchanged in intent
- Sealed RC evidence on an immutable commit
- All 0.4.0 blocking items closed or explicitly waived by freeze amendment
- No future-architecture code mixed into the final cut
