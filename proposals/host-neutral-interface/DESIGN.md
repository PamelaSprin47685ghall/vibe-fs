# Host-neutral interface (HNI) — design

Owner directive (2026-10-21): "制作最优接口，再用适配 opencode/omp，而不是依赖 opencode 接口".
The product core (Execution, Interaction, Mission, Context, Repository, Sphinx, Persistence) talks only to the
ports below. Each port is shaped by **what the product needs**, not by an OpenCode payload. OpenCode and omp
(oh-my-pi 18.12.4) are adapters: wire decode/encode, registration, and nothing else.

Architect lane: eng-host-arch. Registry of every port and its owner: [REGISTRY.md](REGISTRY.md). Work cards: [BOARD.md](BOARD.md).

## 1. Rules

1. **Home.** Ports live in `src/Wanxiangshu/Host/Ports/<Family>.fs(i)`, namespace **`Wanxiangshu.Host.Ports`** (not `Wanxiangshu.Host`:
   the main build is one Fable compilation and many adapter files `open Wanxiangshu.Host` for `CanonicalJson`/`HostDigest`,
   so port names there would shadow `OpenCode.HostSignal`, `SessionMessage`, …), one file per family
   (subfolder `Host/Ports/` so lane-owned domain ports in `Host/*.fs` never collide by path).
2. **No host vocabulary in a port.** No `OpenCode`/`Opencode`/`omp`/`pi` in a port name, member, field, or
   type. No `obj`, no raw JSON object, no `part`/`message.updated`/`chat.params` wire terms. Raw payloads stay in
   the adapter; the port takes and returns records/DUs. Opaque host identifiers travel as typed ids (`SessionId`,
   `ToolCallId`, `TranscriptMessageAddress`, `PromptKey` from `Foundation.Identity`), never as untyped strings the core parses.
3. **Product decisions live in the core.** An adapter may: decode, encode, register handlers, translate errors
   into the port's error DU, and fan a host callback into exactly one port call. An adapter may **not**: choose a
   model, admit/deny a turn or tool, decide retry/abort, compute budgets, render product prose, or keep product
   state (journals, ledgers, leases). Gate HNI-04 measures this.
4. **Capabilities, not version sniffing.** A host declares what it can do in `HostCapabilities` at activation.
   The core degrades on a missing capability explicitly (typed `Unsupported`), never by catching an exception.
5. **Fail closed is a port contract.** Every hook port result DU has an explicit "refuse" case; adapters map it
   to the host's blocking mechanism (OpenCode: throw from hook; omp: `{ block: true, reason }`).
6. **Async = `Task<Result<_, HostError>>`** for host round trips. `HostError = Unsupported of capability |
   NotFound | Refused of reason | Transport of reason | Unknown of reason`. `Unknown` means *outcome not
   observable*: callers must not resend (keeps the existing DetachedSendVerdict.OutcomeUnknown discipline).
7. **Ownership.** Each port has exactly one owner lane (REGISTRY.md). A domain lane may add a port or member for
   its domain: rebase, read REGISTRY.md, add the row in the same commit as the code, reuse existing type names.
   The architect lane reviews shapes and fixes drift (HNI-11).
8. **Clean cutover, no shims.** Existing near-neutral contracts in `Foundation/HostSession*` (`ISessionHostPort`,
   `ISessionSnapshotPort`, `OpenCodePromptOptions`, `OpenCodeChildOptions`, …) are *legacy shapes*. They are
   migrated to the `Wanxiangshu.Host.Ports` names and **deleted in the same change**. There are no compatibility
   type aliases: a legacy name that survives as an alias is a second convention, and the ratchet counts alias
   re-introduction as a regression.

   *Transitional exception (HNI-05, 2026-10-22):* a lane may declare a **lane-owned transitional alias** for a
   legacy shape it has not yet cut over to, when the neutral target does not exist yet — the alias carries the
   lane's own name (`FissionSessionPort`, `FissionEventPort` in `Host/FissionPort.fs(i)`), the lane's product code
   speaks **only** the lane-owned name, and the alias is deleted in the same change that lands the neutral family
   (P-01 / P-07). The alias is a migration seam inside the lane's own port file, not a re-introduction of the
   legacy Foundation name at the product boundary.

## 2. Port families

Notation: `P-xx` ids are stable and used by REGISTRY.md, BOARD.md and gate HNI-04. **The authoritative shapes are the
`src/Wanxiangshu/Host/Ports/<Family>.fsi` files** (declared in HNI-02); the snippets below are summaries. The TS mirror used by
the omp adapter (`adapters/omp/ports.ts`) is kept identical by `adapters/omp/mirror-check.mjs`.

### P-01 Session lifecycle — `Host/Ports/Sessions.fs` : `ISessionPort`
Product need: create a root or child work session under a proved family root, read its parent, list children,
abort/interrupt the running attempt, close a child, find root sessions by title (recovery discovery).
```
type SessionSpec = { Title: string option; Role: AgentRole option; Workspace: WorkspacePath option }
type SessionInfo = { Id: SessionId; Parent: SessionId option; Role: AgentRole option; Title: string option }
ISessionPort:
  Create        : parent: SessionId option * SessionSpec -> Task<Result<SessionId, HostError>>
  Parent        : SessionId -> Task<Result<SessionId option, HostError>>
  Children      : SessionId -> Task<Result<SessionInfo list, HostError>>
  Abort         : SessionId -> Task<Result<unit, HostError>>          // whole session, all attempts
  Interrupt     : SessionId -> Task<Result<unit, HostError>>          // current provider attempt only
  Close         : SessionId -> Task<Result<unit, HostError>>          // child teardown (delete/archive)
  FindByTitle   : string -> Task<Result<SessionId list, HostError>>   // capability: SessionDiscovery
```
Semantics: family-root resolution, managed-child registry and HOST-015 ancestry proof stay in the core
(`Execution/Session`); the port is the raw host verb. Ownership: fork lane (children) with Execution/Session.
- OpenCode: `client.session.create({parentID,title})`, `session.get` (parentID), `session.children`,
  `session.abort`, interrupt = `session.abort` scoped to the running message, `session.delete`, `session.list` + title filter.
- omp: SDK `createAgentSession({ sessionManager, parentSession })` in-process (extension gets the SDK as `pi.pi`),
  `AgentSession.abort()`; interrupt = `ctx.abort()` on the running turn; parent from session header
  `parentSession`; children = sessions created by this plugin (adapter-owned id map, no product logic) plus
  `before_subagent_spawn` observation; close = `session.dispose()`; FindByTitle = `SessionManager.list` +
  `getSessionName()`. Capability `SessionDiscovery` = true.

### P-02 Prompt delivery — `Host/Ports/Prompts.fs` : `IPromptPort`
Product need: deliver one user-role prompt into a session with an optional model route, role, workspace,
correlation key and tool allow-list; learn whether the host accepted it.
```
type PromptRequest = { Text: string; Route: ModelRoute option; Role: AgentRole option;
                       Workspace: WorkspacePath option; Correlation: PromptKey option;
                       ToolPolicy: Map<string, bool> option }
type DeliveryVerdict = OwnedSettled | Refused of reason | OutcomeUnknown of reason   // = DetachedSendVerdict
IPromptPort:
  Send : SessionId * PromptRequest * late: (DeliveryVerdict -> Task) option -> Task<SendOutcome>
```
Semantics: at-most-once. `OutcomeUnknown` must never be resent by the core (dispatch claims stay pending).
Correlation is product metadata echoed back by the host in transcripts (P-03) so dispatch can match deliveries.
Ownership: Interaction/Dispatch.
- OpenCode: `session.promptAsync({ parts:[text], model, agent, tools, metadata })`; late verdict from the detached promise.
- omp: `pi.sendUserMessage(text, { deliverAs })` for the active session, `AgentSession.prompt(text)` for SDK
  children; model via `setModel`, tools via `setActiveTools`; correlation via `pi.appendEntry("wxs.prompt-key", key)`.

### P-03 Transcript read — `Host/Ports/Transcript.fs` : `ITranscriptPort`
Product need: read a session's committed messages as neutral records to recover, project, and locate tool calls.
```
type TranscriptMessage = { Id: TranscriptMessageAddress; Role: Role; Agent: AgentRole option; Route: ModelRoute option;
                           Parent: TranscriptMessageAddress option; CreatedAt: float option; Completed: bool;
                           Terminal: TurnTerminal option; IsCompaction: bool; Correlation: PromptKey option;
                           Parts: TranscriptPart list }
TranscriptPart = Text | Reasoning | ToolUse of ToolCallRecord | File | Other
ITranscriptPort: Messages : SessionId -> Task<Result<TranscriptMessage list, HostError>>
```
Ownership: Execution/Session (replaces `ISessionSnapshotPort`/`SessionMessage`, whose `Model: OpencodeModel` and
`ErrorName: string` are host-shaped). OpenCode: `session.messages` + `SessionSnapshotPort.projectMessage`.
omp: `SessionManager` entries (`message` entries with `AgentMessage` content; `custom` entries for correlation).

### P-04 Tool contribution — `Host/Ports/Tools.fs` : `IToolRegistrar`
Product need: contribute product tools with a JSON-schema contract, run them with session/workspace/cancellation,
return bounded text (+ structured details) and an error flag.
```
type ToolSpec = { Name: string; Description: string; Schema: JsonSchema; Execute: ToolInvocation -> Task<ToolOutcome> }
type ToolInvocation = { Session: SessionId; CallId: ToolCallId; Workspace: WorkspacePath; Args: CanonicalJson;
                  Cancellation: CancellationToken; Progress: (string -> unit) option }
type ToolOutcome = { Text: string; Truncated: bool; IsError: bool; Details: CanonicalJson option }
IToolRegistrar: Register : ToolSpec list -> Result<unit, HostError>     // once, at activation
```
Semantics: names are product names; schema is product-owned; truncation bound is `Host/Contract/ToolResultBound`.
Ownership: tools lane (promotes `OpenCode/Tools/ToolContext.fs` `Tool/ToolContext/ToolOutput`).
- OpenCode: plugin `tool: { [name]: tool({ description, args, execute(args, ctx) }) }`; ctx.sessionID/abort.
- omp: `pi.registerTool({ name, description, parameters: <schema>, execute(id, params, signal, onUpdate, ctx) })`.

### P-05 Tool gates — `Host/Ports/ToolGates.fs` : `IToolGate`
Product need: admit/deny/rewrite a tool call before it runs; observe/replace its result after; shape tool
definitions shown to the model.
```
BeforeTool : ToolCallView -> Task<ToolCallVerdict>   // Allow | Rewrite of CanonicalJson | Refuse of reason
AfterTool  : ToolCallView * ToolOutcome -> Task<ToolResultEdit>   // Keep | Replace of ToolOutcome
DefineTool : ToolDefinitionView -> ToolDefinitionEdit            // Keep | Describe of string * JsonSchema
```
Ownership: tools lane. OpenCode: `tool.execute.before` (throw = refuse, mutate args = rewrite),
`tool.execute.after` (mutate output), `tool.definition`. omp: `tool_call` → `{block, reason, input}`,
`tool_result` → `{content, details, isError}`; DefineTool: omp has no definition hook for built-ins →
capability `ToolDefinitionEdit=false` for built-ins, product tools carry their own description (P-04).

### P-06 Turn transforms — `Host/Ports/TurnTransforms.fs` : `ITurnTransform`
Product need: before each provider request, (a) admit the user turn and choose its route, (b) rewrite the
system prompt, (c) edit the message context (projection, prefix stability, thought surfaces).
```
AdmitTurn       : TurnRequest -> Task<TurnAdmission>   // Admit of ModelRoute option | Refuse of reason
SystemPrompt    : TurnContext * string list -> Task<string list>
ContextMessages : TurnContext * ContextMessage list -> Task<ContextEdit>   // Keep | Replace of ContextMessage list
ProviderParams  : TurnContext * ProviderParams -> Task<ParamsVerdict>       // Accept | Adjust | Refuse
```
`ContextMessage` is the neutral projection already used by `Context/Prefix` (role + parts), not the OpenCode
`{info, parts}` pair. Ownership: plugin-host (wiring) with Context/Interaction (logic).
- OpenCode: `chat.message`, `experimental.chat.system.transform`, `experimental.chat.messages.transform`, `chat.params`.
- omp: `input`/`before_agent_start` (admit; `systemPrompt` replace), `context` (`{messages}`),
  `before_provider_request` (params; payload adjust).

### P-07 Host signals — `Host/Ports/Signals.fs` : `ISignalSource`
Product need: one ordered stream of facts about sessions, attempts, tools and streaming, to drive waits,
quiescence, loop sensing, failure budgets and recovery.
```
HostSignal =
  | SessionCreated of SessionInfo | SessionDeleted of SessionId * parent: SessionId option
  | SessionIdle of SessionId | AttemptStarted of AttemptRef | AttemptFinished of AttemptRef * TurnTerminal
  | ProviderRetry of RetryNotice | ProviderFailed of SessionId * HostFailure | AttemptAborted of SessionId * HostFailure
  | ToolStarted of ToolCallRef | ToolFinished of ToolCallRef * ToolOutcome
  | Streamed of StreamChunk | MessageCommitted of SessionId * TranscriptMessageAddress
ISignalSource: Subscribe : (HostSignal -> Task) -> IDisposable
```
Semantics: per-session order preserved; at-least-once (consumers are idempotent by id, as today).
`TurnTerminal = Completed of finish | Cancelled | Interrupted | Failed of HostFailure`; `HostFailure` carries raw facts
(kind, message, status, host's own retryable claim) — classifying it into `ExecutionFailure`/retry/budget is product logic. Ownership:
plugin-host (source) with Execution/Session (consumers). Promotes `OpenCode/Signals/HostSignal.fs`.
- OpenCode: `event` hook: `session.created/updated/deleted/idle/status/error`, `message.updated`,
  `message.part.updated/delta`.
- omp: `session_start`, `session_shutdown`, `agent_start/agent_end`, `turn_start/turn_end`,
  `message_start/update/end`, `tool_execution_start/end`, `auto_retry_start/end`, `session_stop`.
  Idle = `agent_end` with `ctx.isIdle()`; SessionDeleted = `session_shutdown` for SDK children closed by P-01.

### P-08 Model routing — `Host/Ports/ModelRouting.fs` : `IModelCatalog`
Product need: name a route (provider + model + variant/thinking), list what the host can run, apply a route to a
session or a sub-agent spawn. **Choosing** a route is product logic (`Interaction/Routing`), never adapter logic.
```
type ModelRoute = { Provider: string; Model: string; Variant: string option }
IModelCatalog:
  Available : unit -> Task<Result<ModelRoute list, HostError>>
  Apply     : SessionId * ModelRoute -> Task<Result<unit, HostError>>
SubagentRouting (hook): SpawnRequest -> Task<SpawnRoute>   // Keep | Route of ModelRoute list | Refuse of reason
```
Ownership: plugin-host lane (port) / Interaction routing (decision). Replaces `OpencodeModel` in Foundation.
- OpenCode: `config.provider` / `chat.message` model field; no spawn hook (spawn = P-01 + P-02 route).
- omp: `ctx.modelRegistry`/`ctx.models`, `pi.setModel`, `pi.setThinkingLevel`, `before_subagent_spawn`.

### P-09 Compaction — `Host/Ports/Compaction.fs` : `ICompactionGate`
Product need: contribute context to a host compaction and forbid automatic continuation after it.
```
OnCompacting   : SessionId -> Task<CompactionContribution>   // Context of string list | Replace of prompt
AutoContinue   : SessionId -> Task<ContinueVerdict>           // Allow | Deny of reason
```
Ownership: plugin-host lane with `Host/CompactionPolicy` (already neutral). OpenCode:
`experimental.session.compacting`, `experimental.compaction.autocontinue`. omp: `session.compacting`
(`SessionCompactingResult`), `session_before_compact` (cancel/replace); AutoContinue = `session_stop` result.

### P-10 Persistence touchpoints — `Host/Ports/Storage.fs` : `IHostPaths`
Product need: where product state lives. The product owns its journals/ledgers/leases (Persistence/*); the host
only supplies stable roots and a project identity.
```
IHostPaths: StateRoot : WorkspacePath; ProjectRoot : WorkspacePath; ProjectId : string
```
Rule: no product state in host storage (no OpenCode `storage/`, no omp session `custom` entries) except the
P-02 correlation echo. Ownership: arch lane. OpenCode: `input.directory`, `input.project`, XDG data dir.
omp: `ctx.cwd`, `getAgentDir()`, project trust from `ctx.isProjectTrusted()`.

### P-11 Plugin lifecycle — `Host/Ports/Lifecycle.fs` : `IHostPlugin`
Product need: one activation with the host's ports and capabilities, an agent/role catalog, configuration
validation, and one disposal.
```
type HostCapabilities = { HostName: string; Supports: Set<HostCapability> }
  // HostCapability = ChildSessions | SessionDiscovery | Interrupt | ToolDefinitionEdit | SubagentRouting | ProviderParams | StreamDeltas
type HostPorts = { Sessions: ISessionPort; Prompts: IPromptPort; Transcript: ITranscriptPort; Models: IModelCatalog;
                   Signals: ISignalSource; Tools: IToolRegistrar; Paths: IHostPaths; Diagnostics: IDiagnosticSink;
                   Agents: IAgentCatalog; Capabilities: HostCapabilities }
type ProductHooks = { ToolGate: IToolGate; Turn: ITurnTransform; Compaction: ICompactionGate;
                      Spawn: SpawnRouting option; OnSignal: HostSignal -> Task; Dispose: unit -> Task }
IHostPlugin: Activate : HostPorts -> HostResult<ProductHooks>      // registers product tools via ports.Tools
IAgentCatalog: Roles : unit -> AgentRoleSpec list                  // validation of the catalog is product logic
```
Semantics: the product is a *function from host ports to product hooks*; an adapter is `host API -> HostPorts`
plus `ProductHooks -> host registrations`. HookPolicy criticality (fail-closed for Security/Invariant) is
applied by the core wrapper around `ProductHooks`, not re-implemented per adapter. Ownership: plugin-host lane.
- OpenCode: `Plugin = async (input) => hooks` (`config`, `event`, `dispose`, `tool`, …); agents from `config.agent`.
- omp: extension factory `(pi: ExtensionAPI) => void` registering `pi.on(...)`/`pi.registerTool`;
  agents from omp agent definitions (`omp agents`); dispose on `session_shutdown`.

### P-12 Diagnostics — `Host/Ports/Diagnostics.fs` : `IDiagnosticSink`
`Emit : operation: string * fields: (string * string) list -> unit`. OpenCode: `client.app.log`; omp: `pi.logger`.
Ownership: plugin-host lane.

## 3. Feasibility summary (omp 18.12.4)

Checked offline against `@oh-my-pi/pi-coding-agent@18.12.4` sources
(`~/.local/share/omp-task/vibe/node_modules/@oh-my-pi/pi-coding-agent/src/extensibility/extensions/types.ts`).
Every family has an omp mapping above. Gaps and how the design absorbs them:
- No host-wide child-session tree: P-01 children are those the plugin created (SDK `createAgentSession`); the
  adapter keeps an id map (wire bookkeeping, not product state) — capability flag documents it.
- No `tool.definition` for built-ins: capability `ToolDefinitionEdit=false`; the core's DefineTool edits only apply
  where supported.
- Prompt correlation is not echoed by the host: adapter writes a `custom` entry; P-03 surfaces it as `Correlation`.
Detailed evidence and the adapter skeleton: HNI-03 (`evidence/omp-feasibility.md`, `adapters/omp/`).

## 4. Out of scope
Rendering of TUI widgets, slash commands, keybindings (omp-only UI) — the product has no need for them.
