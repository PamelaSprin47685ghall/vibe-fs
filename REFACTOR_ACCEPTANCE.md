# Refactor acceptance (TASK + 保姆级指南全量 epic)

## 1. Purpose

This document is the **epic tracking SSOT** for closing the **full** refactor scope in `vibe-fs`: **`保姆级重构指南.md` Phase A–G / §7–§8** plus **`TASK.md` §2** progress. It is **not** a narrowed “with-review bundle” PASS list.

- **Epic closure** means: every row in §2 below is either **done** (with verifiable gates) or **explicitly open** with a named follow-up and probe/test where applicable.
- **Green gate**: **`npm run build-and-test`**; passing test count is stated only in **`README.md`「全量口径」**.
- Narrative phase map: **`README.md`「重构进度（对照保姆级指南）」**; smell inventory: **`TASK.md`**.

## 2. Epic checklist (Phase A–G + TASK §2)

| Area | Status target | How to verify |
| --- | --- | --- |
| **Phase A** 命名纠偏 | 完成 | README phase table; no resurrected `Magic*` / legacy names in hot paths. |
| **Phase B** 边界强类型化 | 推进至完成 | `Kernel/` 无业务 `Dyn.*`; `Shell/*Codec` + **`ToolArgsDecode`**; **`ChatHookOutputCodec`** for chat `message.tools`（`ArchitectureTests.chatHooksUsesChatHookOutputCodec`）; **`encodeAgentScalarsRecord`** for agent scalar write-back; hook 输入经 **`OpencodeHookInputCodec`** 等。 |
| **Phase C** 抽公共内核 | 完成 | `MessageTransformPipeline` / `SubagentSpawn` / `MessageTransformHostHooks` 等探针在 `ArchitectureTests.fs` 且登记于 `Tests.fs`。 |
| **Phase D** 统一错误模型 | 推进至完成 | Codec/spawn **`Result<_, DomainError>`**; **`Kernel.ToolResult`** **`wireEncodeResult` / `wireEncodeToolError`** SSOT for tool wire text（`ToolResultWireTests`）; **`ToolCopy.subagentToolFailed`** delegates to **`wireEncodeToolError`**. **Opencode `ReviewTools`**: **`submit_review`** / **`return_reviewer`** decode → **`wireDecodeFailure`**; **`getClientFromPluginCtx`** → **`wireEncodeToolError "OpencodeClient"`**（`opencodeReviewUsesReviewToolsCodec`）. **Opencode `ExecutorTool`** / **`SubagentTools`**: client failure → **`wireEncodeToolError "OpencodeClient"`**（symmetric with ReviewTools; **`opencodeToolsUseWireEncodeForClient`**; no **`formatDomainError`** on client branch). **Opencode `KnowledgeGraphTools`**: **`knowledge_graph_fetch`** / **`return_bookkeeper`** decode → **`wireDecodeFailure`** via **`ToolExecute`**（**`opencodeKgUsesKnowledgeGraphToolsCodec`** extended; no **`formatDomainError`** on decode branch). **Opencode `HookExecute`**: **`apply_patch`** decode → **`wireEncodeToolError "apply_patch"`**（`opencodeHookExecuteUsesPatchToolsCodec`; **`IntegrationToolDefSpecs`**). **Open**: non-decode tool paths may still return prose / **`ToolCopy`** without **`wireEncode*`**; host execute remains **`Promise<string>`**. |
| **Phase E** 收拢可变状态 | 完成 | `RuntimeScope` 注入；`runtimeScopeNoGetDefault` 等探针绿。 |
| **Phase F** KG runtime 拆分 | 完成 | `KnowledgeGraphStorage` / `Workflow` / `BookkeeperLaunch` 切片；`knowledgeGraphRuntimeNoLocalLaunchIfDue`。 |
| **Phase G** 工具 DI | 推进至完成 | **`decodeToolInvocation`** + **`IToolRuntimeContext`** 主路径；`toolArgsDecodeCoversMajorTools`。 |
| **TASK §7** 文案 SSOT | 大体完成 → 完成 | `ToolCatalog` / `ToolCopy` / `subagentToolsUseToolCatalogRequiredKeys`。 |
| **TASK §8** 权限语义化 | 大体完成 | `ToolPermission` + `canUseForHost` 双宿主。 |
| **TASK §2.1** `obj`/`Dyn` | 推进 | 工具+chat tools 过滤经 Map codec；**`mergeConfigObj`** 仅合并用户 obj 与 **`encodeAgentScalarsRecord`** 产物（`withRoleDefaultsFor` 不再 empty merge + setKey _SCALAR 链）。余：chat/command hook **payload** 仍为 `obj` 边界。 |
| **TASK §2.2** 双宿主重复 | 切片 SSOT | 无单体 `HostAdapter` 直至 API 稳定（指南 §5.3）；`MESSAGING_WIRE.md` 记录 wire 分叉。 |
| **TASK §2.3** 错误与 wire | 推进 | 内核 **`ToolResult`** 统一 **`{context} failed: {formatDomainError}`**；decode 热路径（含 **Opencode ReviewTools** / **ExecutorTool** / **SubagentTools**（client → **`wireEncodeToolError "OpencodeClient"`**，**`opencodeToolsUseWireEncodeForClient`**）/ **KnowledgeGraphTools**（**`knowledge_graph_fetch`** / **`return_bookkeeper`** → **`wireDecodeFailure`**，**`opencodeKgUsesKnowledgeGraphToolsCodec`**）/ **HookExecute apply_patch**、Mux **HostTools** / **WebTools** / **ReviewToolsMux**) 经 **`wireDecodeFailure`** / **`wireEncodeToolError`**。宿主工具返回仍为 **`Promise<string>`**；非 decode 失败未全收敛。 |
| **TASK §2.6** 工具 DI | 大体完成 | 非指南 §9.2 全量 `Async<ToolResult>` 容器前，以 context + codec 为 SSOT。 |

**Operational note:** “全量不打折” = 上表 + README 阶段表诚实边界；未完成项必须 appear in this table as **open**, not hidden behind a narrowed PASS.

## 3. Intentional boundaries (not automatic FAIL)

| Item | Rationale |
| --- | --- |
| **Dual-host messaging wire fork** | `MESSAGING_WIRE.md`; Opencode `info`+`parts` vs Mux flat message. |
| **Host `obj` beyond message wire** | `HOST_OBJ_BOUNDARY.md`; hook payloads, config tree, tool `args`/`context` inventory. |
| **Opencode `ToolSchema` Zod vs Mux `mkSchema`** | 描述/必填键 SSOT 在 `ToolCatalog`; 形状层双轨保留。 |
| **Monolithic `HostAdapter`** | Deferred per 保姆级 §5.3 until host APIs stabilize. |
| **Full `Async<ToolResult>` execute signature** | Epic item; current wire is string + `ToolResult` encode helpers. |
| **`ArchitectureTests.fs` size** | Maintainability; probes remain registered in `Tests.fs`. |

## 4. Authority order

1. **`TASK.md`** — warehouse smell list and epic wording.
2. **`保姆级重构指南.md`** — phase intent A–G.
3. **`REFACTOR_ACCEPTANCE.md` (this file)** — epic row status vs open items.
4. **`README.md`「重构进度」+「全量口径」** — implementation map and **only** green test count.
5. **`MESSAGING_WIRE.md`** — messaging fork supplement.
6. **`HOST_OBJ_BOUNDARY.md`** — §2.1 `obj` inventory and convergence (non-message surfaces).