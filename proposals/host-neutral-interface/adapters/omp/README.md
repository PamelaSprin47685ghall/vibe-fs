# omp adapter skeleton (HNI-03)

Thin adapter from omp 18.12.4's extension API to the host-neutral ports. Not shipped; it proves the ports are
implementable on a second host and keeps the TS mirror honest.

- `ports.ts` — **generated** mirror of `src/Wanxiangshu/Host/Ports/*.fsi` (plus the two foundation signature
  files). Do not edit by hand; regenerate with `node gen-ports.mjs --write`.
- `gen-ports.mjs` — the generator: parses the F# signature files (records, DUs, abstract members, generic aliases)
  and emits the TypeScript mirror mechanically.
- `mirror-check.mjs` — regenerates the mirror and fails on **any** byte difference, so a renamed field, a changed
  field type, a lost `option`, a missing or extra union case, a changed member signature or a TS-only extra is
  caught. It does not compare names only.
- `adapter.ts` — `createOmpHostPorts` (omp → `HostPorts`), `registerProductHooks` (`ProductHooks` → `pi.on`/`registerTool`), `activateOnOmp`.
- `sdk-children.ts` — child sessions through the omp SDK (`createAgentSession`, `SessionManager`).
- `session-files.ts` — the one place that binds omp's own session-file lookup (`readSessionInfo`) to the reader
  `sdk-children.ts` needs. It is a separate module so the offline fixture can import `sdk-children.ts` without
  pulling the omp package into the process.
- `fixtures/` — offline events and a fake `ExtensionAPI`.

## What is actually proven

- The generated mirror is **byte-identical** to a mechanical derivation of the F# signatures.
- `adapter.ts`, `sdk-children.ts` and the fixture **type-check** against the omp 18.12.4 type definitions
  (`tsc --noEmit`) and against the generated mirror.
- The fixture drives the adapter offline and passes.

What is **not** proven: no omp process is started, no model is called, and no behaviour against a real host is
exercised. The adapter is a type-checked skeleton, not a wired host.

## Deliberate host bindings

Two F# types have no direct TypeScript equivalent; the mapping is a documented binding, not a mechanical
equivalence:

- `System.Threading.CancellationToken` → `AbortSignal`
- `System.IDisposable` → `IDisposableHandle` (`{ dispose(): void }`)

## Refusals instead of silent loss

A port request the host cannot honour is answered with a typed `HostError` (`Unsupported`, `Refused`, `NotFound`),
never with a silent success and never by dropping part of the caller's request. Examples: `Models.Apply` refuses a
session that is not the host's own (omp can only set the current session's model); `Prompts.Send` refuses
`Role`/`Route`/`Workspace`/`ToolPolicy` on **both** delivery paths, because neither omp delivery call can carry
them (`sendUserMessage` for the host session, `children.prompt` for a child — the latter takes text only); the
context hook leaves a message untouched when a part has no plain-text omp encoding.

The `Prompts.Send` refusal is **path-scoped**: the reason names the path that cannot carry the field
(`omp child prompts carry text only: cannot carry Role` vs `omp cannot carry Role`), so a child refusal is never
read as a host-session refusal.

`Prompts.Send` is split into two phases, because the two phases have different failure meanings:

- **Preparation** (the unsupported-field check, then the `wxs.prompt-key` correlation entry) happens before the
  host is asked to accept the prompt. A failure here is `Fatal` — definitely not sent. The unsupported-field
  check runs *before* the correlation write, so a prompt that is refused never leaves a phantom correlation
  entry in the session log.
- **Delivery** is the point of no return: a throw there is `AcceptanceUnknown`, because the host may already
  have taken the prompt.

The `late` callback is invoked **outside** the delivery `try`, so a throw from the caller's own callback cannot
make a settled delivery look ambiguous — it propagates to the caller unchanged. `late({ OwnedSettled })` fires
**only** for an `AdmittedWithReceipt` outcome: a `Retryable` outcome (the host refused) fires no late verdict at
all, because nothing was settled.

## The transport receipt is admission-shaped

`Prompts.Send` returns `AdmittedWithReceipt` with a receipt of the form `accepted-omp-<sessionId>`.

This is a contract, not a cosmetic choice. The F# side says a transport receipt is **admission-shaped**: the Host
prompt-send endpoint hands back an `accepted-*` admission receipt, and a receipt that does not look like one
means the Host returned something else and the caller must decide
(`src/Wanxiangshu/Foundation/Identity.fs`; the same rule is restated in `src/Wanxiangshu/Foundation/Outcome.fs`).
Every host the product itself drives already emits that shape — `accepted-<sessionId>` for the OpenCode host and
`accepted-detached-<promptKey>` for a detached send — so the omp adapter emits the same shape rather than a
private `omp:` namespace.

What the receipt asserts, and what it does not:

- It asserts the host **took the prompt for delivery**. It is deliberately *not* a message identity: the F# keeps
  receipts in their own type precisely so that an `accepted-*` receipt can never be mistaken for a physical
  message id and promoted to an authority root.
- The **child** path is observable: `children.prompt` resolves to a boolean that says whether the host took the
  prompt (`false` → `Retryable`), and rejects if the call itself failed.
- The **host** path is **not** observable. `ExtensionAPI.sendUserMessage` returns `void` — omp hands the text to
  the host session and does not report back, and a rejected delivery is swallowed by omp itself (it logs it
  internally). The adapter therefore cannot distinguish "enqueued" from "enqueued and then failed", and the
  receipt does not claim more than "the host took it". This is a limit of the omp extension API, not of this
  adapter, and it is stated here rather than papered over with a synthetic check.

`Sessions.Parent` is one more: omp records `SessionHeader.parentSession` **verbatim**, and its own callers pass
either a session id or a session **file path**. A path-shaped value is resolved through the host's own
session-file lookup (`readSessionInfo`); when the file does not resolve, the request is refused with
`HostError.Refused` rather than returning a path masquerading as a `SessionId`.

## Which session id is the neutral `SessionId`

omp has **two** session ids and they are not always the same:

- the **persisted** id — `SessionManager.getSessionId()`, the id of the session file. It is the one
  `parentSession`, the session listings and the session files refer to.
- the **provider** id — `AgentSession.sessionId`, which is `#activeProviderSessionId()`: it prefers a fresh
  or inherited provider id over the session manager's, so it can differ from the persisted one.

The neutral `SessionId` is always the **persisted** id. The adapter therefore takes the id from
`sessionManager.getSessionId()` everywhere — including for the `session.compacting` and `session_stop` events,
whose own `sessionId` / `session_id` fields carry the *provider* id, and for a child session's own id, which
omp exposes as `AgentSession.sessionId`. Using the provider id would make the same session appear under two
different ids in the product, and would break the link to the `parentSession` a child was created with.

The offline fixture covers this: the events and the fake child session carry a provider id
(`provider-session-id`) that differs from the persisted one (`root` / `persisted-session-id`), and the fixture
asserts that the product sees the persisted id.

Run: `./check.sh` (needs the omp-task vibe install for the types, a TypeScript compiler — set `TSC` or let the
script discover one — and `bun`). No omp process, no model calls.
