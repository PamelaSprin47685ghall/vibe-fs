# Host `obj` boundary: inventory and convergence

Kernel stays **`obj`-free** (`ArchitectureTests.kernelBoundary`: no `Dyn.`, `createObj`, `box`, `unbox`, `open Shell`). Host plugins still speak JavaScript objects. This document inventories where `obj` remains **on purpose** or **in flight**, complements **`MESSAGING_WIRE.md`** (message wire only), and maps **`TASK.md` §2.1** to concrete modules and gates.

## Layer rule

| Layer | `obj` policy |
| --- | --- |
| **`src/Kernel/`** | Forbidden as a type carrier; domain = records, DUs, `Map`, `Result<_, DomainError>`. |
| **`src/Shell/`** | **Single decode/encode beachhead**: `Dyn` + `*Codec` + `ToolArgsDecode` + `ToolRuntimeContext`. |
| **`src/Opencode/`**, **`src/Mux/`** | Hook signatures and host callbacks stay `obj`; business logic consumes **`IToolRuntimeContext`**, **`ToolExecutionContext`**, **`DecodedToolInvocation`**, or small records from Shell codecs. |

Do not push `Dyn.get` into Kernel or duplicate codec logic in host folders—**architecture probes** enforce that for hot paths.

## Categories

### 1. Tool execute arguments

- **Wire**: host passes `args: obj` into each tool `execute`.
- **SSOT (done)**: **`Shell.ToolArgsDecode.decodeToolInvocation`** → **`Kernel.ToolArgs`** or **`CoderBatch` / `InvestigatorBatch`** (`toolArgsDecodeExists`, `toolArgsDecodeCoversMajorTools`, `decodedToolInvocationNoObj`).
- **Per-tool codecs** (called from `ToolArgsDecode` or host-specific entry): `FileToolsCodec`, `WorkBacklogToolsCodec`, `WebToolsCodec`, `FuzzyToolsCodec`, `ExecutorToolsCodec`, `ReviewToolsCodec`, `KnowledgeGraphToolsCodec`, `PatchToolsCodec`, `SubagentSimpleArgsCodec`, `SubagentIntentsCodec` (via `intentsRawFromArgs`).
- **Failure wire**: **`Shell.ToolExecute`** (`wireDecodeFailure`, `wireDomainFailure`, `wireEncodeToolError`) — probes `toolExecuteWireHelperExists`, `muxHostToolsWireDecodeFailures`, `muxWebToolsUsesWireDecodeFailure`, Opencode symmetric paths.
- **Intentional**: host **`Promise<string>`** success path still returns plain text, not a serialized `ToolResult` record (`TASK §2.3` epic).

### 2. Tool / session context (`config` / `context`)

- **Wire**: Mux `config: obj`, Opencode plugin `context: obj` on tool execute and many hooks.
- **SSOT (done)**: **`Shell.ToolContextCodec`** (`decodeOpencodeToolContext`, `decodeMuxConfig`) → **`Kernel.ToolContext.ToolExecutionContext`**; **`Shell.ToolRuntimeContext`** (`fromOpencode`, `fromMuxConfig`) → **`IToolRuntimeContext`** (directory, session, workspace, **`AbortSignal`** on Shell side only — `toolRuntimeContextAbortFromShellCodec`).
- **Probes**: `opencodeSubagentToolsUsesFromOpencode`, `muxHostToolsFuzzyUsesFromMuxConfig`, `muxReviewUsesFromMuxConfig`, etc. (see `tests/ArchitectureTests.fs`).

### 3. Hook input (chat, command, tool lifecycle, message transform)

- **Wire**: entire hook payload remains **`input: obj`** / **`output: obj`** because host event types are not versioned F# records.
- **SSOT (partial — field extractors, not full payloads)**:
  - **Opencode**: **`Shell.OpencodeHookInputCodec`** — session/tool/args/agent/command/event envelope, message-transform agent resolution (`resolveMessagesTransformAgent`, `agentFromMessageInfo`). Consumed by `HookExecute`, `ChatHooks`, `CommandHooks`, `MessageTransform`, `SessionLifecycleObserver`, `EventHooks`, `ToolDefinitionHooks` (probes: `opencodeHookExecuteUses…`, `chatHooksUsesChatHookOutputCodec` for **output** tools map only).
  - **Mux**: **`Shell.MuxHookInputCodec`** — `decodeMuxMessagesTransformInput`, `decodeMuxToolExecuteAfterInput`, read-only executor via **`ExecutorToolsCodec.peekExecutorMode`** (`muxMessageTransformUsesMuxHookInputCodec`, `muxHookInputCodecExecutorReadOnlyUsesCodec`).
- **Open (`TASK §2.1`)**: no typed **`ChatHookInput`** / **`CommandHookInput`** records; hooks still thread raw `obj` between host and Shell helpers.

### 4. Agent config tree (Opencode)

- **Wire**: host `agent` / `mcp` / permission / tools trees are nested **`obj`** graphs.
- **Read path (partial)**: **`Shell.OpencodeAgentConfigCodec.decodeUserAgentScalars`** → typed scalars + **`PermissionOverrides` / `ToolsOverrides` as `Map`**; **`ChatHookOutputCodec`** reads/filters **`message.tools`** as `Map`, writes via **`toolsMapToObj`**.
- **Write / merge path (intentional `obj`)**: **`Shell.OpencodeAgentConfigWire`** — **`mergeConfigObj`**, **`applyAgentConfigFor`**, **`disableMimoMemoryAndCheckpoint`**, agent-map walks with **`Dyn.keys` / `Dyn.get`** (`agentConfigUsesOpencodeAgentConfigWire`; probe `arch: wire owns mergeConfigObj`).
- **Next convergence**: encode full agent node from typed maps in one shot (`encodeAgentScalarsRecord` already reduces scalar write-back); shrink **`mergeConfigObj`** to private assign helpers, not public merge API (`REFACTOR_ACCEPTANCE` §2 TASK §2.1).

### 5. Messaging wire (session messages)

- **Fork documented in `MESSAGING_WIRE.md`** — not duplicated here.
- **Shared**: **`Shell.MessagingPartCodec`**, **`Shell.MessagingEncodeHelpers`**, **`Shell.ChatTransformOutputCodec`** for projection output.
- **Per-host**: **`Opencode/MessagingCodec.fs`**, **`Mux/MessagingCodec.fs`** keep top-level envelope differences (`messagingWireForkDocumented`, `dualHostMessagingCodecUsesEncodeHelpers`).
- **Policy**: **do not merge** wire codecs until hosts align; extend Shell part logic only.

### 6. Tool schema generation (host-only shape layer)

- **Opencode**: **`Opencode/ToolSchema.fs`** — Zod DSL at compile time; descriptions/required keys from **`Kernel.ToolCatalog`** (`opencodeToolSchemaDescriptionsFromCatalog`, `subagentToolsUseToolCatalogRequiredKeys`).
- **Mux**: **`Shell.MuxJsonSchema`** + **`Mux.Wrappers.mkSchema`**.
- **Intentional**: shape builders stay host-specific; SSOT is catalog text + required keys, not one JSON schema type.

### 7. Dependencies / runtime injection (`deps`, plugin `ctx`)

- **Mux hooks**: second argument **`deps: obj`** (e.g. `directory`, `workspaceId`) — decoded in **`MuxHookInputCodec`** / **`MuxWorkspaceCodec`**, not in Kernel.
- **Opencode**: **`Shell.OpencodeClientCodec`** (`getClientFromPluginCtx`, `getSessionApiFromClient`) — replaces inline **`Dyn.get ctx "client"`** (`opencodeNoDirectClientSessionDyn`, `opencodeSubagentToolsUsesOpencodeClientCodec`).
- **Runtime state**: **`Shell.RuntimeScope`** (caps, iterators, session queues, projection) — injected per registration, not global `obj` bags (`runtimeScopeNoGetDefault`).

## Shell codec module index (boundary SSOT)

| Module | Role |
| --- | --- |
| `ToolArgsDecode` | Tool `args` → `ToolArgs` / subagent batches |
| `ToolContextCodec` / `ToolRuntimeContext` | `context` / `config` → execution context |
| `OpencodeHookInputCodec` / `MuxHookInputCodec` | Hook field extraction |
| `OpencodeAgentConfigCodec` / `OpencodeAgentConfigWire` | Agent read vs merge/write |
| `ChatHookOutputCodec` | Chat `message.tools` Map ↔ obj |
| `MessagingPartCodec` / `MessagingEncodeHelpers` | Message parts (both hosts) |
| `ChatTransformOutputCodec` | Message-transform projection encode |
| `MuxWorkspaceCodec` / `MuxAiSettingsCodec` | Mux workspace + AI settings |
| `OpencodeClientCodec` / `OpencodeContextCodec` | Plugin client + abort |
| `OpencodeSessionPromptCodec` / `OpencodeSessionSpawnCodec` | Subagent session IO |
| `FileToolsCodec` / `WorkBacklogToolsCodec` / `PatchToolsCodec` / `DelegateToolsCodec` | File, todo, patch, delegate |
| `WebToolsCodec` / `FuzzyToolsCodec` / `ExecutorToolsCodec` | Web, fuzzy, executor |
| `ReviewToolsCodec` / `KnowledgeGraphToolsCodec` | Review + KG tools |
| `SubagentIntentsCodec` / `SubagentSimpleArgsCodec` | Subagent intents + simple args |
| `HostMessagePartCodec` | `msg.parts` text / read dedup |
| `BacklogSessionCodec` / `SubagentIntentsCodec` | Backlog + intents wire |

Supporting: **`Shell.Dyn`**, **`Shell.DynField`** (shared field readers for codecs only).

## Intentional remaining `obj` (not bugs)

| Surface | Why it stays |
| --- | --- |
| Host hook `input` / `output` signatures | Host ABI; typed only at extracted fields. |
| **`mergeConfigObj`** + agent map mutation | Host config is a mutable JS tree; full typed config record is epic work. |
| **`toolsMapToObj`** / **`permissionMapToObj`** on write | Wire must remain JSON-like objects for the host UI. |
| Dual-host **`MessagingCodec`** envelopes | See **`MESSAGING_WIRE.md`**. |
| Zod / `mkSchema` tool parameters | Compile-time host DSL; catalog SSOT for copy + required keys. |
| Tool `execute` return **`Promise<string>`** | Documented wire contract; errors use **`ToolResult`** encode helpers. |

## Convergence plan (TASK §2.1)

| Step | Status | Notes |
| --- | --- | --- |
| Kernel `Dyn`-free | **Done** | `kernelBoundary` |
| Major tools via **`decodeToolInvocation`** | **Done** | `toolArgsDecodeCoversMajorTools` |
| Context via **`IToolRuntimeContext`** on hot paths | **Done** | Phase G probes |
| Chat **`message.tools`** via **`ChatHookOutputCodec`** | **Done** | `chatHooksUsesChatHookOutputCodec` |
| Hook **helpers** via `*HookInputCodec` | **Done** for listed Opencode/Mux consumers | Payload still `obj` |
| Agent scalar write-back | **In progress** | `encodeAgentScalarsRecord`; merge tree remains |
| Typed chat/command hook payloads | **Next** | Extend `OpencodeHookInputCodec` with records per hook kind |
| Single typed agent config encode | **Next** | Reduce public **`mergeConfigObj`** |
| Unified messaging wire | **Deferred** | Host protocol change required |
| Monolithic **`HostAdapter`** | **Deferred** | `保姆级重构指南` §5.3; slice SSOT instead |

## Architecture probes (reference)

Probes live in **`tests/ArchitectureTests.fs`** and are registered from **`tests/Tests.fs`**. Use them as the machine-readable supplement to this doc:

- **Kernel**: `kernelBoundary`, `kernelNoEmptyDefault`
- **Tool args / wire**: `toolArgsDecodeExists`, `toolArgsDecodeCoversMajorTools`, `decodedToolInvocationNoObj`, `toolExecuteWireHelperExists`, host-specific `*UsesToolArgsDecode`, `*Uses*Codec`, `*UsesFromOpencode` / `*UsesFromMuxConfig`
- **Hooks**: `chatHooksUsesChatHookOutputCodec`, `opencodeHookExecuteUsesOpencodeHookInputCodec`, `muxMessageTransformUsesMuxHookInputCodec`, `muxPluginToolExecuteAfterUsesMuxHookInputCodec`
- **Agent config**: `agentConfigUsesOpencodeAgentConfigWire`, `agentConfigUsesOpencodeAgentConfigCodec`
- **Messaging**: `messagingWireForkDocumented`, `dualHostMessagingCodecUsesEncodeHelpers`, `messagingPartCodecExists`
- **Host isolation**: `opencodeNoMuxRef`, `opencodeNoDirectClientSessionDyn`

When adding a new host-facing field, add or extend a **Shell codec** and an **architecture probe** before using `Dyn.get` in `Opencode/` or `Mux/`.

## Authority order

After **`MESSAGING_WIRE.md`**, treat **`HOST_OBJ_BOUNDARY.md`** as the §2.1 supplement for all non-message `obj` surfaces. Epic row status: **`REFACTOR_ACCEPTANCE.md` §2** (`TASK §2.1`).