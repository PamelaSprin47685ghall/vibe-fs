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
`Role`/`Route`/`Workspace`/`ToolPolicy` for the host session because `sendUserMessage` cannot carry them; the
context hook leaves a message untouched when a part has no plain-text omp encoding.

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
