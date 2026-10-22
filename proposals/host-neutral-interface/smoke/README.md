# CUT-36 — real omp session smoke (opt-in)

This directory holds the **opt-in real** smoke for CUT-36 §3: proving that the
compiled Fable product (`wanxiangli` `Work`/`Ownership`/`Resources` + the product
fork/join/deliver path) really runs inside a **real second host process** — here
`omp` 18.12.4 — and that the four physical facts are observable as *physical*
events, not as booleans the product reports about itself.

The default gates (`npm run check`, `scripts/verify.mjs`, unit tests) stay
**offline**: nothing in this directory is wired into them, and the runner
refuses to start without an explicit opt-in flag.

## Files

| file | role |
| --- | --- |
| `run-smoke.mjs` | opt-in runner: launches a real `omp` process, then asserts |
| `assert-smoke.mjs` | the assertions (host-observed and on-disk facts only); also runnable standalone to re-check evidence later |
| `observer-extension.mjs` | smoke-owned **read-only** omp extension that records the host's own events (provider payloads, session ids, tool executions) as JSONL |
| `assert-smoke.test.mjs` | **offline** tests for the assertions (no omp, no quota) |

## Exact invocation

```bash
cd /home/hermes/work/cut36-omp/vibe-fs

# Every compile on this host goes through the build lock.
# Incremental; drop --clean unless strictly required.
flock /home/hermes/work/vfs-build.lock node scripts/build.mjs

# the smoke itself compiles nothing: it only loads the already built bundle,
# so it runs OUTSIDE the build lock
CUT36_OMP_REAL_SMOKE=1 \
  node proposals/host-neutral-interface/smoke/run-smoke.mjs
```

The build lock (`/home/hermes/work/vfs-build.lock`) serialises compiles on
this host. The smoke scripts never compile (no `dotnet`, no Fable
compile, no `scripts/build.mjs` invocation inside the runner); the bundle is
a build artifact that must already exist, and the runner refuses to start
(exit 2) when it is missing.

The runner invokes the host itself as:

```
omp -p "<prompt with the run nonce>" \
    -e <repo>/dist/Composition/Omp/OmpExtension.js \
    -e <repo>/proposals/host-neutral-interface/smoke/observer-extension.mjs \
    --cwd <repo> --mode json
```

with `OMP_SMOKE_OBSERVER_OUT=<tmp>/cut36-omp-observer.jsonl` in the child
environment. There is no `OMP_CANARY` arming flag and no product report: the
product writes nothing for the smoke.

Environment overrides:

| var | default | meaning |
| --- | --- | --- |
| `CUT36_OMP_REAL_SMOKE` | unset | must be `1`; otherwise the runner exits 2 without any host start |
| `CUT36_OMP_BUNDLE` | `<repo>/dist/Composition/Omp/OmpExtension.js` | built product extension bundle |
| `CUT36_OMP_WORKSPACE` | repo root | workspace root passed to `omp --cwd` |
| `CUT36_OMP_OBSERVER_OUT` | `<tmp>/cut36-omp-observer.jsonl` | host-observation JSONL |
| `CUT36_OMP_PROMPT` | "Use the product's delegation tool once to deliver the token {nonce} to a child session, then reply with the single word CANARY_DONE." | the single real turn; `{nonce}` is substituted |
| `CUT36_OMP_NONCE` | random per run | the run-specific delivery/join token |
| `CUT36_OMP_TIMEOUT_MS` | `600000` | hard timeout on the real host run |
| `CUT36_OMP_TOOL_PATTERN` | `^wxs_deliver$` (case-insensitive) | which observed tool calls count as the product's tool; the product's ONE tool is `wxs_deliver` (`HostPlugin.fs:191`), so the default is exact — a broad pattern would let an unrelated built-in host tool (e.g. `task`) satisfy the product-artifact fact |

### Offline invocation (no model call)

The assertions have their own offline test suite — one case per asserted fact
and per provider payload shape, plus the negative cases for an **inert**
bundle and for **stale** ownership (24 cases) — and the evidence can be
re-checked later without spending quota. The suite is not wired into
`npm test`: the default gates stay offline and the real smoke stays an
explicit opt-in.

```bash
node --test proposals/host-neutral-interface/smoke/assert-smoke.test.mjs

node proposals/host-neutral-interface/smoke/assert-smoke.mjs \
  --observer <tmp>/cut36-omp-observer.jsonl \
  --summary <tmp>/cut36-omp-smoke-summary.json
```

## Cost

**Two** provider requests per run, on the existing monthly-quota gateway
model configured in `~/.omp/agent/config.yml` (role `default` → `gateway/lazy/default`):
the parent's own turn, plus the child session that the product's `wxs_*`
delegation tool drives to completion inside that same tool call.
No `~/.omp` settings are modified and no API key is created. The smoke is the
only thing in this workstream that spends quota.

## What is asserted

Every fact is **host-observed or on disk** — the product writes no smoke
report, so there is no self-report that could agree or disagree. Each fact
below is checked against the host's own events or the product's own files.

1. **The product artifact really loaded** — the host's own `session_start`
   event (session id + cwd), the parent session id must be this run's, and
   the **product tool must be advertised by the host to the provider** in
   the `before_provider_request` payload.

   The `session_start` event alone is **not** the product artifact: the
   smoke's own observer extension is always loaded, so it fires
   `session_start` by itself. A bundle that loads but is **INERT** (a
   default-exported function that does nothing) registers no tool, so the
   provider is never told about `wxs_deliver` — the run FAILs on
   "host loaded the product bundle (product tool advertised to the
   provider)". An unrelated **built-in** host tool (`task`, …) does not
   satisfy it either: the pattern is exactly `^wxs_deliver$`.
2. **A real provider request really happened** — a `before_provider_request`
   payload was captured and an assistant `turn_end` with a `stopReason` exists.
3. **The product's paired-bilingual resources reached the provider** — the
   *system prompt* must contain **both** the English and the Chinese sentence
   read from the product's own compiled resources
   (`resources/provider/world/common-law/{en,zh-CN}.md`), and the **product
   tool's** description (the one matching `^wxs_deliver$`) must itself contain
   both scripts. The user prompt
   is excluded from the inspected text and is explicitly asserted NOT to carry
   the resource markers, so a bilingual prompt cannot fake the fact.
   The provider payload is parsed in **every shape it can take** — pi-ai
   `Context` (`systemPrompt: string[]`), Anthropic (top-level `system`, as a
   string or `[{type:"text"}]` blocks), OpenAI Responses (`instructions`) and
   OpenAI chat completions (`role: system|developer` messages) — and the tool
   list is read from the Anthropic (`{name, description}`), Responses
   (`{type:"function", name, description}`) and chat-completions
   (`{type:"function", function:{…}}`) shapes alike. The fact therefore holds
   whichever provider the run went through.
4. **A real child session was forked** — among the session files created by
   *this run only*, the child's header `parentSession` must equal this run's
   parent session id. Old sessions are never scanned.
5. **The delivery really happened** — the child's own transcript must contain
   the delivered user message **with this run's nonce**, and an assistant reply.

   The child is created through the product's own `ISessionPort.Create` → omp
   `SessionManager.open(..., {parentSession})` → `createAgentSession`, and the
   parent's join result is the `wxs_*` tool's `tool_execution_end` text — so a
   tool that returns before the child replies fails fact 4c.
6. **The join really happened** — the parent's observed product tool call
   (`wxs_deliver`, or whatever name matches `CUT36_OMP_TOOL_PATTERN`) must
   have a `tool_execution_end` result containing the same nonce, and **no**
   product tool call may have `isError`.
7. **Work/Ownership are real on-disk artifacts, and they belong to THIS run** —
   the product state root must exist, the durable Work journal
   (`<state root>/wanxiangshu/work.sqlite`) must exist **and be readable**,
   and the Ownership registry must be **committed inside that same journal**.

   Ownership is not a file: `DurableWorkHost.executeOwnershipDurable`
   appends the registry record to the stream `"ownership:" + registryId`
   in the same `journal_entries` table the product's Work records use
   (`wanxiangli/eventstore/SqliteJournal`). The smoke therefore **reads** the
   sqlite file (read-only, `node:sqlite`) and **discovers** the stream whose
   name starts with `ownership:` — the registry id is not knowable in
   advance and is never hard-coded. At least one such stream must exist with
   at least one non-empty committed record.

   **Run scoping.** "An `ownership:` stream exists" is *not* evidence: an
   earlier run of the same product in the same workspace left one behind, and
   a workspace that never ran the product can carry one from another project.
   The runner therefore takes a **pre-run baseline snapshot** of the journal
   (max revision per stream) *before* the host starts, and the facts are
   scoped to THIS run by two independent facts:

   - **"Ownership scope is bound to THIS run's workspace"** — the ownership
     record's `scopeId` must be `host-activation:<workspace>`. The product
     opens the scope `host-activation:` + `ports.Paths.ProjectId`
     (`HostPlugin.fs:159`) and the omp adapter's project id **is** the cwd
     it was started in (`Adapter.fs:838-840`).
   - **"durable Work stream for THIS run's child session exists"** /
     **"… was written by THIS run"** — the product opens the child's durable
     work under the id `host2-<sha256(childSessionId)[0:32]>`
     (`HostPlugin.fs:230`, `HostPlugin.fs:88-89`). The child session id is
     fresh for every run, so no earlier run can have produced that stream;
     "written by this run" means its revision is **greater than** the
     pre-run baseline's.

   **"Ownership registry was committed by THIS run"** accepts *either* a
   fresh ownership revision *or* proof through the run's own (never
   repeatable) child work stream: the product's activation is
   **idempotent** — re-opening an already open scope is `Observed`, which
   writes no new journal row — so a second run in the same workspace leaves
   the ownership revision untouched, and only the per-session work stream
   proves the run.

   **"Work journal carries this run's identifier"** is likewise satisfied by
   the run's own work stream, or by a nonce inside a record that is **newer
   than the baseline**. A nonce left behind by an earlier run does not
   satisfy it. (The SecondHost delivery path does not journal the delivered
   prompt — that lives in the product's OpenCode deliver tool — so the
   work-stream identity is the identifier the journal can actually prove.)

   A missing, unreadable or corrupt journal **fails the fact** — it is
   never skipped, because the file's presence makes the fact decidable.
   The same holds for the baseline: with **no pre-run baseline** (or an
   unreadable one) "written by THIS run" is undecidable, so the fact FAILS
   closed rather than passing on a plausible guess.

   The state root is resolved exactly as the product resolves it
   (`RuntimePath.stateRoot`): the **primary worktree**'s `.Herdchestra`,
   found via `git worktree list --porcelain` (with
   `git rev-parse --git-common-dir` for the topology). It is *not* inside a
   `.git` directory, and a linked worktree's state is the primary worktree's
   state. The resolution **fails closed**: if git cannot resolve the
   topology, the run FAILs ("cannot resolve state root") instead of guessing
   a path.

A missing fact is a hard failure: the runner exits non-zero and prints which
fact was not observed.

When the run fails, the failure message also carries the host's diagnostics —
the last 20 lines of omp's stderr and the last 20 lines of the host's own log
file (`~/.omp/logs/omp.<date>.<pid>.log`) — so a slow or failed activation is
diagnosable from the run's own output. A missing bundle is different: it is
detected before the host starts and exits 2 with no host run at all.

## Evidence produced

| artefact | path |
| --- | --- |
| host observations | `<tmp>/cut36-omp-observer.jsonl` |
| host stdout (JSONL events) | `<tmp>/cut36-omp-stdout.jsonl` |
| verdict summary (incl. nonce, parent session id, this run's session files) | `<tmp>/cut36-omp-smoke-summary.json` |
| real child session transcript | `~/.omp/agent/sessions/<workspace-slug>/<ts>_<id>.jsonl` |
| product state root artifacts | `<primary worktree>/.Herdchestra/...` |
| durable Work journal (Ownership + run-scoped work-stream evidence) | `<primary worktree>/.Herdchestra/wanxiangshu/work.sqlite` |
| pre-run journal baseline (max revision per stream, taken before the host starts) | in the verdict summary JSON (`journalBaseline`) |
| host log | `~/.omp/logs/omp.<date>.<pid>.log` |

## Scope note

The observer extension is smoke-owned and read-only: it records events and
writes one JSONL file. It makes no product decision, does not touch product
state, and never throws into the host. The product extension itself is
activated normally by the host when loaded — the opt-in flag protects only the
real-model smoke path, not extension activation.
