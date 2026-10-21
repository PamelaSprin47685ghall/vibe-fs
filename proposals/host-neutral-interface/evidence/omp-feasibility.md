# HNI-03 — omp 18.12.4 feasibility (offline, no model calls)

Source of truth: `@oh-my-pi/pi-coding-agent@18.12.4` as installed on the box by omp-task
(`~/.local/share/omp-task/vibe/node_modules/@oh-my-pi/pi-coding-agent`, `package.json` version checked by `check.sh`);
binary `~/.local/bin/omp` reports `omp/18.12.4`. Extension entry: `omp -e <file>` / `--hook`, factory `(pi: ExtensionAPI) => void`
(`src/extensibility/extensions/types.ts`, interface `ExtensionAPI`, 2250 lines). Nothing was executed against a model.

## Port-by-port

| Port | omp surface used | Verdict |
|---|---|---|
| P-01 Sessions | SDK `createAgentSession({cwd, sessionManager})`, `SessionManager.newSession({parentSession})`, `AgentSession.abort/prompt/dispose/sessionId`, `SessionManager.list`, `getHeader().parentSession`, `readSessionInfo` (session file → session id), `ctx.abort()` | Implementable. omp has no host-wide child tree: children = sessions this plugin created (adapter id map in `sdk-children.ts`, wire bookkeeping only). `Interrupt` and `Abort` both map to `abort()` (omp has no finer attempt abort). `parentSession` is recorded **verbatim** by omp and its own callers pass either a session id (`session-manager.ts:1272`, `:2466`) or a session **file path** (`agent-session.ts:12416` `previousSessionFile`, `session-manager.ts:3938` `sourceSessionFile`), so a path-shaped value is resolved through `readSessionInfo` and, when it does not resolve, refused with `HostError.Refused` instead of being returned as a `SessionId` (open item O-4). |
| P-02 Prompts | `pi.sendUserMessage(text, {deliverAs})` (active session), `AgentSession.prompt` (children), `pi.appendEntry` for correlation, `setModel`, `setActiveTools` | Implementable. `sendUserMessage` is fire-and-forget (void): verdict `OwnedSettled` after enqueue; transport exceptions → `AcceptanceUnknown` (never resent). |
| P-03 Transcript | `ctx.sessionManager.getEntries()` (`type: "message"` → `AgentMessage`), child `session.messages` | Implementable. Message ids are positional (`omp:<index>`); omp entry ids exist (`SessionEntry.id`) and should replace them when wiring for real (open item O-1). |
| P-04 Tools | `pi.registerTool({name, label, description, parameters, execute(toolCallId, params, signal, onUpdate, ctx)})` | Implementable; `parameters` takes a JSON-schema object (TypeBox `TSchema` is JSON schema). |
| P-05 Tool gates | `tool_call` → `{block, reason, input}`; `tool_result` → `{content, details, isError}` | Implementable for Before/After. `DefineTool` for built-ins has no omp hook → capability `ToolDefinitionEdit` not advertised. |
| P-06 Turn transforms | `input` (`{handled}` refuses), `before_agent_start` (`{systemPrompt}`), `context` (`{messages}`), `before_provider_request` (`payload`) | Implementable. `ProviderParams` must edit the provider payload (provider-specific JSON) — adapter-side encode only (open item O-2). |
| P-07 Signals | `session_start`, `session_shutdown`, `turn_start/turn_end`, `agent_end` (+`ctx.isIdle()`), `auto_retry_start`, `tool_execution_start/end`, `message_update` | Implementable. omp is in-process, so per-session order is the event order. |
| P-08 Model routing | `ctx.modelRegistry.getAvailable/find`, `pi.setModel`, `before_subagent_spawn` (`{model, block, reason}`) | Implementable; omp is the only host with a spawn hook (capability `SubagentRouting`). |
| P-09 Compaction | `session.compacting` (`{context, prompt}`), `session_stop` (`{continue, reason}`) | Implementable. |
| P-10 Storage | `ctx.cwd`, agent dir | Implementable. |
| P-11 Lifecycle | extension factory, `session_start` (activation with a live `ctx`), `session_shutdown` (dispose) | Implementable; activation is deferred to the first `session_start` because `ExtensionContext` only exists inside handlers. |
| P-12 Diagnostics | `pi.logger` | Implementable. |

## Conformance evidence (`adapters/omp/check.sh`)
1. `tsc --noEmit` (strict) of `ports.ts`, `adapter.ts`, `sdk-children.ts` and the fixture against omp's own
   `dist/types` — passes. The compiler is the repo-local TypeScript **7.0.2** (`node_modules/.bin/tsc`); the earlier
   "TypeScript 5.9.3" claim in this document was wrong. Mutation check: returning `{ block: 3 }` from `tool_call` and
   reading `e.sessionIdX` from `session_stop` both fail type-check (TS2769, TS2551), so the check really binds to
   omp's types.
2. `mirror-check.mjs`: `ports.ts` is **generated** from the F# port signatures (`gen-ports.mjs` parses
   `src/Wanxiangshu/Host/Ports/*.fsi` plus the two foundation signature files) and the check regenerates it and
   **byte-diffs** the result, failing with the differing lines printed. A renamed field, a changed field type, a
   lost `option`, a missing or extra union case or a changed member signature all break it — the earlier
   name-only comparison could not see any of those.
3. `fixtures/fixture.test.ts` (bun, offline): a fake `ExtensionAPI` replays recorded-shape omp events
   (`fixtures/events.json`) through the adapter to a fake product; 17 assertions (tool registration and execution
   encode, refuse→block, rewrite/allow, result replace, input refuse, system prompt, compaction context,
   auto-continue deny, turn_end error → `AttemptFinished Failed`, transcript decode, child create, model apply
   error, prompt correlation, and the three parent-session forms: session id, resolvable file path, unresolvable
   file path → refusal).

## Open items (tracked on BOARD.md)
- O-1 use omp `SessionEntry.id` for `TranscriptMessageAddress` instead of positional ids.
- O-2 `ProviderParams` encode per provider family (payload shape differs per API).
- O-3 the real binding needs the product compiled to JS (Fable output) exposing an `IHostPlugin`; the skeleton proves the
  port shapes, not the product wiring (that is HNI-08's `HostPorts -> ProductHooks` refactor).
- O-4 `SessionHeader.parentSession` is recorded verbatim by omp and may hold a session **file path** rather than a
  session id. The adapter resolves a path-shaped value through `readSessionInfo` and refuses it when the file does
  not resolve; a real binding must decide whether an unresolvable parent path is an error or a legitimately
  deleted parent session. omp's own listing layer surfaces the very same field as
  `SessionInfo.parentSessionPath` (`session/session-listing.ts:488`), i.e. omp itself does not know whether the
  recorded value is an id or a path — so the ambiguity is the host's, not the adapter's, and the adapter must not
  guess.
