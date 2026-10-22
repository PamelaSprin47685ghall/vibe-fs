# HNI-03 — omp 18.12.4 feasibility (offline, no model calls)

Source of truth: `@oh-my-pi/pi-coding-agent@18.12.4` as installed on the box by omp-task
(`~/.local/share/omp-task/vibe/node_modules/@oh-my-pi/pi-coding-agent`, `package.json` version checked by `check.sh`);
binary `~/.local/bin/omp` reports `omp/18.12.4`. Extension entry: `omp -e <file>` / `--hook`, factory `(pi: ExtensionAPI) => void`
(`src/extensibility/extensions/types.ts`, interface `ExtensionAPI`, 2250 lines). Nothing was executed against a model.

## Port-by-port

| Port | omp surface used | Verdict |
|---|---|---|
| P-01 Sessions | SDK `createAgentSession({cwd, sessionManager})`, `SessionManager.newSession({parentSession})`, `AgentSession.abort/prompt/dispose/sessionId`, `SessionManager.list`, `getHeader().parentSession`, `readSessionInfo` (session file → session id), `ctx.abort()` | Implementable. omp has no host-wide child tree: children = sessions this plugin created (adapter id map in `sdk-children.ts`, wire bookkeeping only). `Interrupt` and `Abort` both map to `abort()` (omp has no finer attempt abort). `parentSession` is recorded **verbatim** by omp and its own callers pass either a session id (`session-manager.ts:1272`, `:2466`) or a session **file path** (`agent-session.ts:12416` `previousSessionFile`, `session-manager.ts:3938` `sourceSessionFile`), so a path-shaped value is resolved through `readSessionInfo` and, when it does not resolve, fails with `HostError.NotFound` carrying the raw recorded value — see the decided rule **O-4** below. |
| P-02 Prompts | `pi.sendUserMessage(text, {deliverAs})` (active session), `AgentSession.prompt` (children), `pi.appendEntry` for correlation, `setModel`, `setActiveTools` | Implementable. `sendUserMessage` is fire-and-forget (void): verdict `OwnedSettled` after enqueue; transport exceptions → `AcceptanceUnknown` (never resent). |
| P-03 Transcript | `ctx.sessionManager.getEntries()` (`type: "message"` → `AgentMessage`), child `session.messages` | Implementable. The address is the omp entry id (`SessionEntry.id`), never a positional index — see the decided rule **O-1** below. |
| P-04 Tools | `pi.registerTool({name, label, description, parameters, execute(toolCallId, params, signal, onUpdate, ctx)})` | Implementable; `parameters` takes a JSON-schema object (TypeBox `TSchema` is JSON schema). |
| P-05 Tool gates | `tool_call` → `{block, reason, input}`; `tool_result` → `{content, details, isError}` | Implementable for Before/After. `DefineTool` for built-ins has no omp hook → capability `ToolDefinitionEdit` not advertised. |
| P-06 Turn transforms | `input` (`{handled}` refuses), `before_agent_start` (`{systemPrompt}`), `context` (`{messages}`), `before_provider_request` (`payload`) | Implementable. `ProviderParams` is encoded per provider family by the adapter and fails closed — see the decided rule **O-2** below. |
| P-07 Signals | `session_start`, `session_shutdown`, `turn_start/turn_end`, `agent_end` (+`ctx.isIdle()`), `auto_retry_start`, `tool_execution_start/end`, `message_update` | Implementable. omp is in-process, so per-session order is the event order. |
| P-08 Model routing | `ctx.modelRegistry.getAvailable/find`, `pi.setModel`, `before_subagent_spawn` (`{model, block, reason}`) | Implementable; omp is the only host with a spawn hook (capability `SubagentRouting`). |
| P-09 Compaction | `session.compacting` (`{context, prompt}`), `session_stop` (`{continue, reason}`) | Implementable. |
| P-10 Storage | `ctx.cwd`, agent dir | Implementable. |
| P-11 Lifecycle | extension factory, `session_start` (activation with a live `ctx`), `session_shutdown` (dispose) | Implementable; activation is deferred to the first `session_start` because `ExtensionContext` only exists inside handlers. |
| P-12 Diagnostics | `pi.logger` | Implementable. |

## Conformance evidence (`adapters/omp/check.sh`)

**Current check status (post-O-3, post-P-19).** The historical strict `tsc --noEmit` conformance check listed in
item 1 passed **before** upstream P-19 added the required `Config` port; the **current** strict check **fails**
with `TS2741: Property 'Config' is missing in type ... but required in type 'HostPorts'` (see the O-3 blocker
paragraph below) and no fake `Config` implementation was fabricated. The `mirror-check.mjs` byte-diff (item 2)
**passes**, and the offline latch fixture (item 3) **passes with 49 assertions**. Only the strict type-check is
red, and only on the `Config` binding gap.
1. `tsc --noEmit` (strict) of `ports.ts`, `adapter.ts`, `sdk-children.ts`, `session-files.ts` and the fixture against omp's own
   `dist/types` — **historical (pre-P-19) pass**. At the time this was recorded the strict check was green
   against omp's own types. The compiler is the repo-local TypeScript **7.0.2** (`node_modules/.bin/tsc`); the
   earlier "TypeScript 5.9.3" claim in this document was wrong. **Historical** mutation check (also pre-P-19):
   returning `{ block: 3 }` from `tool_call` and reading `e.sessionIdX` from `session_stop` both failed
   type-check (TS2769, TS2551), so the check really binds to omp's types. **This check is no longer green:**
   the current strict run fails on the missing `Config` binding — `TS2741: Property 'Config' is missing in type
   ... but required in type 'HostPorts'` — because upstream P-19 made `Config: IHostConfigPort` a required
   `HostPorts` member and omp exposes no configuration-mutation surface to bind it (see the O-3 blocker
   paragraph). No mock or pass-through `Config` was fabricated.
2. `mirror-check.mjs`: `ports.ts` is **generated** from the F# port signatures (`gen-ports.mjs` parses
   `src/Wanxiangshu/Host/Ports/*.fsi` plus the two foundation signature files) and the check regenerates it and
   **byte-diffs** the result, failing with the differing lines printed. A renamed field, a changed field type, a
   lost `option`, a missing or extra union case or a changed member signature all break it — the earlier
   name-only comparison could not see any of those.
3. `fixtures/fixture.test.ts` (bun, offline): a fake `ExtensionAPI` replays recorded-shape omp events
   (`fixtures/events.json`) through the adapter to a fake product; **49 assertions at the current tip**, all of
   which pass. The **34** enumerated below are the **historical** tip of this lane's earlier adapter
   work (tool registration and execution
   encode, refuse→block, rewrite/allow, result replace, input refuse, system prompt, compaction context,
   auto-continue deny, turn_end error → `AttemptFinished Failed`, transcript decode, child create, model apply
   error, prompt correlation, the three parent-session forms: session id, resolvable file path, unresolvable
   file path → refusal, and the provider-vs-persisted session id: the compaction hooks and a child session must
   carry the persisted id, and the provider id must not be registered as a session).

## Decided rules (was: open items)

HNI-13 turns O-1..O-5 into rules. They are **binding on `cut36-omp-2`** (the lane that builds the real omp
adapter): that lane implements them, it does not re-decide them. Every omp claim below is grounded in the
installed `@oh-my-pi/pi-coding-agent@18.12.4` sources; where a claim is inferred from a type rather than
observed at runtime, it says so.

### O-1 — `TranscriptMessageAddress` is the omp entry id

**Decision.** `TranscriptMessageAddress` **is** the omp `SessionEntry.id` (the persisted entry id); the adapter
never synthesises `omp:<index>`, and an entry with no usable id fails the read with `HostError.Transport`
carrying the reason instead of returning an unstable address.

**The child path uses the same entry ids.** The rule covers the child path as well as the host path. The child's
transcript is read from the child's **own** `AgentSession.sessionManager.getEntries()` — the same `SessionEntry`
union and the same ids — and `session.messages` **must not** be used as an address source: it yields
`AgentMessage` objects that carry no entry id (`AgentSession.messages` returns `this.agent.state.messages`,
`src/session/agent-session.ts:6946-6948`), so an adapter reading it cannot produce an entry-id address at all.
The skeleton's child seam has the same shape today — `OmpChildSessions.messages` returns `AgentMessage[]`
(`sdk-children.ts:62`) — while the `AgentSession` itself already holds the session manager
(`AgentSession.sessionManager`, `src/session/agent-session.ts:840`; already used by the skeleton at
`sdk-children.ts:44`), so the child path can read entry ids without any new omp API.

**State of the reference skeleton.** The skeleton in `adapters/omp/` **conforms to this rule**. `decodeMessage`
now takes the entry id (`adapter.ts`, `decodeMessage(m, id)`) and the transcript port reads the omp **entries**:
the host path from `ctx().sessionManager.getEntries()` (keeping the `type === "message"` entries) and the child
path from the child's own session manager through the `OmpChildSessions.entries` seam
(`sdk-children.ts`, `AgentSession.sessionManager.getEntries()`), so both paths address messages by entry id.
An entry with a missing or empty id fails the read with `HostError.Transport` whose reason names the entry's
position; no adapter source contains a synthesised `omp:<index>` address any more. The offline fixture exercises
this for real: it reads the session, appends a third entry and reads again, asserting the first two addresses are
unchanged, asserts that an entry with an empty id fails with `HostError.Transport` naming the position, and
asserts that a child session's transcript yields entry-id addresses. The conformance was **executed** offline
(the fixture ran and passed); it was **not** exercised against a running omp host.

**Why.** `SessionEntryBase.id` is a **required** `string` (`src/session/session-entries.ts:79-97`, interface
`SessionEntryBase`; the union `SessionEntry` at `:328` is built from it), so every entry omp persists has an id
by construction; ids are minted by `generateId` (`src/session/session-migrations.ts:5`) and legacy v1 sessions
are given ids during migration (`migrateV1ToV2`, `src/session/session-migrations.ts:14-40`). `getEntries()`
returns those entries verbatim — `SessionManager.getEntries(): SessionEntry[]`
(`src/session/session-manager.ts:3823`, a shallow copy of the entry list), and the read-only view omp exposes to
extensions keeps it (`ReadonlySessionManager`, `src/session/session-manager.ts:720-744`, picks `getEntries`).
A positional id is not stable: any append shifts the later messages' indices, so a transcript address would
stop meaning the same message. The address is the product's handle on a message (`P-03`, `DESIGN.md` §2), so it
must be stable across reads.

**Acceptance.**
1. Read a session's entries, append one more entry, read again: the addresses of the earlier messages are
   unchanged. A positional id fails this (the last message's index shifts).
2. An entry whose `id` is missing/empty produces a typed refusal (`HostError.Transport` with the reason), never
   a fabricated address — and the refusal names the entry's position so the failure is diagnosable.
3. No adapter source contains the literal `omp:` prefix in an address.
4. **A child session's transcript addresses are also entry ids** (the child's `sessionManager.getEntries()`),
   and the same stability check (acceptance 1) holds for a child. The positional `session.messages` path
   **cannot** satisfy acceptance 1: it exposes no entry ids, so an address derived from it is positional by
   construction.

**Re-open trigger.** omp changes the entry identity model (e.g. `SessionEntryBase.id` becomes optional or
composite) so that one entry has several stable ids.

### O-2 — provider params encode is adapter-only, per provider family, fail-closed

**Decision.** Encoding `ProviderParams` into the provider payload is **adapter-only and per provider family**:
the adapter holds an explicit table of the provider families it can encode, advertises capability
`ProviderParams` **only** for those families, and refuses (fails closed) any request it cannot map completely;
the port carries a provider payload only as `ProviderParams.Extra` (`CanonicalJsonText`), which the adapter
passes through byte-for-byte.

**Why.** The port shape is deliberately payload-free (`ProviderParams` = `Temperature`/`TopP`/`MaxOutputTokens`/
`Reasoning`/`Extra`, `src/Wanxiangshu/Host/Ports/TurnTransforms.fsi:34-39`; the TS mirror `ports.ts:282-288`),
and omp's hook carries an **opaque** payload: `BeforeProviderRequestEvent.payload: unknown`
(`src/extensibility/extensions/types.ts:1054-1057`), whose value the handler may replace wholesale
(`ExtensionRunner.emitBeforeProviderRequest`, `src/extensibility/extensions/runner.ts:2103-2138`). The wire
field names differ per family, so a generic encoder cannot exist: openai-completions uses `temperature`/`top_p`
(`pi-ai/src/providers/openai-completions.ts:2054-2057`) and `max_tokens` **or** `max_completion_tokens` depending
on the endpoint (`:2201-2203`); openai-responses uses `temperature`/`top_p`
(`pi-ai/src/providers/openai-shared.ts:4091-4092`) and `max_output_tokens` (`:4082`); anthropic-messages uses
`max_tokens`/`temperature`/`top_p` plus a `thinking` object (`pi-ai/src/providers/anthropic-wire.ts:328-342`);
google uses `generationConfig.temperature`/`maxOutputTokens`/`topP`
(`pi-ai/src/providers/google-shared.ts:896-898`) and `thinkingConfig.thinkingBudget`/`thinkingLevel`
(`pi-ai/src/providers/google-gemini-cli.ts:1286-1294`). The family key is omp's `Model.api`
(`pi-catalog/src/types.ts:1572`; the known set `KnownApi` at `:9-25`, mirrored by `pi-ai/src/api-registry.ts:20-35`).
Applying *part* of a verdict (e.g. temperature but not the token cap) would silently change the model's
behaviour, so the adapter refuses instead.

**State of the reference skeleton.** The skeleton in `adapters/omp/` does **not implement this rule**. It
registers no `before_provider_request` handler and holds no per-family encoder table, and it therefore does
**not** advertise the `ProviderParams` capability: the `Capabilities.Supports` set the adapter hands to the
product is exactly `ChildSessions`, `SessionDiscovery`, `Interrupt` and `SubagentRouting` (`adapter.ts`,
`createOmpHostPorts`). That is **fail-closed at activation** in the sense of acceptance 2 — the product is
told up front that the capability is absent — but it is **not** a partial encode: nothing is encoded at all.
Acceptance 1 (the parametrised per-family encoder test), acceptance 3 (refusing an incompletely mappable
verdict) and acceptance 4 (`ProviderParams.Extra` reaching the payload byte-for-byte) are **unimplemented**;
they are owned by `cut36-omp-2`. The rule was **not** exercised against a running omp host.

**Acceptance.**
1. For every family in the adapter's table, a parametrised test asserts the encoded payload contains each
   mapped field. Mutation: drop one field from the encoder and the test fails.
2. A provider family that is **not** in the table is reported as `Unsupported` at **activation** (the
   `HostCapabilities` the adapter hands to the product), not discovered at request time.
3. A `ProviderParams` verdict the family cannot map completely is refused as a whole; the adapter never applies
   a subset.
4. `ProviderParams.Extra` reaches the payload byte-for-byte (the exact `CanonicalJsonText` string appears
   unchanged in the encoded payload).

**Re-open trigger.** omp exposes a provider-parameter hook with a **typed, family-neutral** parameter object
(so the adapter would no longer need a per-family table).

### O-3 — the binding contract between the Fable-built product and omp

**Decision.** The omp entry module is **hand-written and product-free**, and the Fable-built product is passed
in as a plain `IHostPlugin` value (no reflection, no dynamic import of product internals); the contract is
specified as follows.

1. **What the Fable output exports.** A single value shaped like `IHostPlugin`
   (`src/Wanxiangshu/Host/Ports/Lifecycle.fsi:37-38`: `abstract Activate: ports: HostPorts -> HostResult<ProductHooks>`;
   TS mirror `ports.ts:437-439`). The entry module receives it as an ordinary argument; it never reflects over
   it, never imports product internals, and never looks up product symbols at runtime.
2. **The hand-written entry module omp loads.** `export default (pi) => activateOnOmp(pi, plugin, sdkChildSessions(...), agents)`
   — omp loads an extension module's **default export** as the factory (`getExtensionFactory`,
   `src/extensibility/extensions/loader.ts:62-67`) whose type is
   `ExtensionFactory = (pi: ExtensionAPI) => void | Promise<void>`
   (`src/extensibility/extensions/types.ts:2054`). The module contains wiring only: it builds the child-session
   seam, calls `activateOnOmp`, and holds **no product decision** (no admission rule, no retry, no budget, no
   prose, no state).
3. **When activation happens.** On the **first `session_start`**, because `ExtensionContext` exists only inside
   handlers — the context is a handler parameter
   (`ExtensionHandler<E, R> = (event: E, ctx: ExtensionContext) => …`, `src/extensibility/extensions/types.ts:1610`;
   `ExtensionContext` at `:671`) and the factory itself only receives `pi`
   (`ExtensionAPI`, `:1624`). omp emits `session_start` from the runtime initialisation
   (`src/modes/runtime-init.ts:212`) and in ACP mode (`src/modes/acp/acp-agent.ts:2664`).
4. **How the SDK pieces are obtained.** From `ExtensionAPI.pi`, which is typed
   `pi: typeof PiCodingAgent` (`src/extensibility/extensions/types.ts:1642`) and is injected as the package
   namespace itself (`new ConcreteExtensionAPI(PiCodingAgent, …)`, `src/extensibility/extensions/loader.ts:451`;
   `PiCodingAgent` is the package root, `src/extensibility/extensions/loader.ts:29`). The package root re-exports
   the SDK (`export * from "./sdk"`, `src/index.ts:40`, giving `createAgentSession`,
   `src/sdk.ts:1735`) and the session manager (`export * from "./session/session-manager"`, `src/index.ts:52`,
   giving `SessionManager`, `src/session/session-manager.ts:890`). **So "the SDK is `pi.pi`" is true in 18.12.4.**
5. **Disposal.** `session_shutdown` (`src/extensibility/shared-events.ts:105-107`) calls `hooks.Dispose` once.
6. **Idempotency.** Activation must not register the product hooks twice when `session_start` fires again: the
   entry module keeps a single activation latch and the second `session_start` does not re-register handlers.
   The latch is necessary because a handler registered *during* an event emission is not called for that pass
   (omp reads the handler list per extension when the event is emitted,
   `src/extensibility/extensions/runner.ts:1664-1668`; `on` appends to the extension's list,
   `src/extensibility/extensions/loader.ts:210-214`) — *inferred from these two sources*, not observed at runtime.
7. **Failure semantics.** An activation error is **logged and degraded**: the adapter reports the failure
   through `pi.logger` and returns without throwing into omp. omp itself treats a throwing factory as a load
   failure (`src/extensibility/extensions/loader.ts:445-458`) and reports handler errors through the runner's
   error channel (`emitError`, `src/extensibility/extensions/runner.ts:1317`); the adapter must not convert a
   product failure into a host failure.

**State of the reference skeleton.** The skeleton in `adapters/omp/` **partly implements this rule**. Rules 1–5
and 7 are satisfied by the skeleton's shape: the product is passed in as a plain `IHostPlugin` value and
activated through its single `Activate` entry point (`adapter.ts`, `activateOnOmp`); the wiring is
hand-written and imports no product module; the SDK pieces come from `ExtensionAPI.pi`; `session_shutdown`
disposes the product; and a failing activation is logged through `pi.logger` and degraded rather than thrown
into omp. **Rule 6 (the activation latch) now conforms**: `activateOnOmp` keeps a single latch, the omp
handlers are registered exactly once per extension and read the live product hooks out of a slot, so a second
`session_start` neither activates the product again nor appends a second handler list entry. The offline
fixture asserts this for real — `Activate` is called once across two `session_start`s, a handler-driven event
after the second `session_start` reaches the product exactly once, `session_shutdown` disposes exactly once
across two shutdowns, and a `session_start` after the shutdown activates a fresh product and announces exactly
one more session. **Acceptance 5 does not exist**: no gate rejects a product decision in the entry module, and
the skeleton has **no entry module at all** (only `activateOnOmp`), so `cut36-omp-2` owns both the entry
module and the gate. The rule was **not** exercised against a running omp host.

**The `Config` binding blocker (exact, unfabricated).** Upstream P-19 declared `Config: IHostConfigPort` as a
**required** member of `HostPorts` (`src/Wanxiangshu/Host/Ports/HostConfig.fsi`, mirrored into `ports.ts`), so the
port object the skeleton hands to `Activate` no longer type-checks: `tsc --noEmit` fails with
`TS2741: Property 'Config' is missing in type ... but required in type 'HostPorts'` (`adapter.ts`, the `ports`
literal). This is the **only** current strict-check failure, and it is a real missing prerequisite, not a
rounding error: **omp exposes no configuration-mutation surface to an extension.** `ExtensionAPI`
(`src/extensibility/extensions/types.ts`) has no settings get/set/write method — the port's
read-with-provenance and persisted-write semantics have nothing on the omp side to bind to. The only candidate,
omp's internal settings registry (`@oh-my-pi/pi-coding-agent/config/settings`), is not part of the extension
surface **and is not loadable in this runtime**: importing it aborts with
`Cannot find module '.../@oh-my-pi/pi-natives@18.12.5/native/pi_natives.linux-x64-baseline.node'`, i.e. the
native module the settings layer depends on is absent from the execution environment. Per the rule's own failure
semantics (rule 7: the adapter must not invent host behaviour), **no mock, no-op, or always-succeed `Config`
port was fabricated.** The `Config` port therefore remains unbound, and closing it needs either an omp extension
settings API or a runtime that can load `pi-natives` — a prerequisite this lane cannot satisfy.

**Acceptance (for `cut36-omp-2`).**
1. A golden-package test loads the **real** entry module with a fake `pi` and a **stub `IHostPlugin`**: the
   plugin's `Activate` is called exactly once, with the `HostPorts` the adapter built.
2. Firing `session_start` twice registers the product hooks exactly once (no duplicate handler list entries).
3. A plugin whose `Activate` returns an error is logged and degraded: omp receives no throw, and the session
   continues.
4. Firing `session_shutdown` calls `hooks.Dispose` exactly once.
5. A review rule (gate) rejects any product decision appearing in the entry module: the entry module contains
   no admission/refusal/retry/budget/prose logic and imports no product module. **No such gate exists today** —
   `cut36-omp-2` must add it as part of this rule's implementation.

**Re-open trigger.** omp changes the extension entry contract (a different factory shape, an explicit
activation event, or a host-provided plugin slot) — or the product's `IHostPlugin` shape changes such that
`Activate` is no longer the single entry point.

### O-4 — a path-shaped `parentSession` is resolved through the host's lookup, or refused

**Decision.** A path-shaped `parentSession` is resolved through the host's own session-file lookup; when it
does not resolve, the port call fails with `HostError.NotFound` whose reason contains the **raw** recorded
value; the adapter never returns a path as a `SessionId` and never guesses "no parent".

**State of the reference skeleton.** The skeleton in `adapters/omp/` **conforms to this rule**. The adapter's
error guard maps `ParentUnresolvedError` to `HostError.NotFound` (`adapter.ts`, `guard`), with the raw recorded
value carried verbatim in the message, and the offline fixture asserts exactly that — an unresolvable path-shaped
parent now fails with `NotFound` (previously `Refused`). The behaviour for a recorded **session id** and for a
**resolvable** path is unchanged. The conformance was **executed** offline (the fixture ran and passed); it was
**not** exercised against a running omp host.

**Why.** omp records the parent **verbatim** and does not normalise its kind: `SessionHeader.parentSession?: string`
(`src/session/session-entries.ts:52`) — a bare `string` with no id/path distinction. omp's own callers put
different kinds in it: a session id (`session-manager.ts:1272`, `parentSession: previousSessionId`; `:2466`)
and a session **file path** (`session-manager.ts:3938`, `parentSession: sourceSessionFile`). omp's own listing
layer surfaces the same field as `SessionInfo.parentSessionPath` (`src/session/session-listing.ts:488`) — i.e. omp
itself does not know whether the recorded value is an id or a path. The lookup that can tell is
`readSessionInfo(file): Promise<SessionInfo | undefined>` (`src/session/session-listing.ts:845`): `undefined`
means the file is unreadable or is not a session. **Consequence for HOST-015** (the ancestry proof in
`Execution/Session`, `DESIGN.md` §2 P-01): an unresolvable parent must surface as a **hard failure at the
family-root check**, not as a root session — a session whose ancestry cannot be established must never be
treated as a root, because that would let it pass the proof. **Trade-off:** a legitimately deleted parent looks
like an error. Failing closed is right because the alternative (treating the child as a root) breaks the
ancestry proof silently; the error is loud, typed, and carries the raw value so the operator can act.

**Acceptance.**
1. `Parent` on a session whose recorded parent is a **session id** returns that id.
2. `Parent` on a session whose recorded parent is a **resolvable file path** returns the id that
   `readSessionInfo` reports for that file.
3. `Parent` on a session whose recorded parent is an **unresolvable file path** fails with `HostError.NotFound`
   whose reason contains the raw recorded value verbatim.
4. No port call ever returns a value that looks like a path in a `SessionId` position.
5. In the core, an unresolvable parent is a hard failure at the family-root check — never a root session.

**Re-open trigger.** omp normalises `parentSession` to a single kind (a typed id) in a future release, so the
path resolution branch becomes dead code.

### O-5 — the neutral `SessionId` is always the persisted id

**Decision.** The neutral `SessionId` is always the **persisted** id (`sessionManager.getSessionId()`); omp's
provider id (`AgentSession.sessionId`, and the `sessionId`/`session_id` fields some events carry) never crosses
the port, and every callback that carries a provider id is translated by the adapter before it becomes a port
value.

**Why.** omp has two session identities and they differ. The persisted id is
`SessionManager.getSessionId(): string` (`src/session/session-manager.ts:3159`) — the id of the session file,
the one `parentSession` and the session listings refer to. The provider id is `AgentSession.sessionId`
(`src/session/agent-session.ts:7074`), which returns `#activeProviderSessionId()`
(`:5707-5709`: `freshProviderSessionId ?? providerSessionId ?? sessionId ?? sessionManager.getSessionId()`) and
therefore prefers a fresh or inherited provider id. The events carry the provider one: `session.compacting`
is emitted with `sessionId: this.#host.sessionId()` (`src/session/session-maintenance.ts:3656`; the host hook
`sessionId(): string` at `:431`, bound to `this.sessionId` at `src/session/agent-session.ts:1900`) and
`session_stop` with `session_id: this.sessionId` (`src/session/agent-session.ts:5432`). The provider id is
omp's **routing identity** (prompt cache, credential/account pool — see
`src/session/agent-session.ts:5739-5753`), not a session identity: a product that tracked it would see the
same session change its id mid-life.

**Acceptance.**
1. A fixture where the persisted id and the provider id differ: every emitted signal, transcript value and
   session value carries the **persisted** id.
2. The provider id appears nowhere in the port surface (grep-able over the adapter's port-facing code: the
   only session ids that cross are `sessionManager.getSessionId()` results and ids the adapter minted itself).
3. The provider id is not registered as a session (e.g. it never becomes a key in the adapter's child-session
   map, so `Abort`/`Close` on it is `NotFound`).

**Re-open trigger.** omp makes the provider id and the persisted id identical by construction (one id for
both), which would make the translation rule a no-op.

### Not decided here

The five rules above decide the **port-facing semantics**. They do not decide the product's own behaviour
(which model, whether a turn is admitted, retry/budget policy) — that stays in the core, per `DESIGN.md` §1
rules 3–5.
