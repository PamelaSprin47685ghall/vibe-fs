# Agent — 目标实现

## Implements

行为合同见 `what/agent.md`；本文件只描述目录装配、权限计算和可见面生成算法。

## Ownership

模块边界与写入口见 `shape/agent.md`。

---

## 启动装配

1. 枚举 AGENT-002 **二十二名**（含 `fast|deep-bookkeeper`）；缺一 fail fast。不得装入 `meditator`/`executor`/`student`/`teacher` 及 `fast|deep-*` 旧对。
2. 校验 `peer(fast)=deep` 对称；model 字符串非空且 pair 内互异。
3. 非法旧名（AGENT-004，含 `meditator`/`executor`/`student`/`teacher` 及裸名）在配置验证阶段拒绝，无 alias。
4. 静态校验 SyncDelegate DAG（AGENT-024）无环：仅允许
   `Inquiry|Coder|DevOps → Inspector` 与 `DevOps → Coder`。
5. SyncDelegate tier：owner effective tier 确定性映射到 delegate tier；配置不得提供 per-call 覆盖。
6. PersonaCatalog（AGENT-028）：`Role × initial selected tier → SessionPersona` 在 Session 创建路径 **resolve-once** 冻结；Fallback / Strength / Peer 只改 ExecutionBinding，不得重绑 Persona（AGENT-029）。Bookkeeper Persona（Clerk/Curator）同表，仍为 InternalLeaf。

---

## 权限对象

1. 由 `CanonicalRole` 导出角色工具集 → Host-final permission + ToolRegistry 两层（AGENT-007）。
2. Role 未定 → 模型可见插件工具集为空。
3. `external_directory="allow"` 仅经 `StaticTools.permissionObj` → `ManagedAgentConfig.applyOwnedFields` 写入每个 managed agent（AGENT-019）。
4. 禁止全局 permission 顶替 agent 级写入。
5. `InvocationMode = SynchronousDelegate` 时：callee 普通 Assistant completion 结束；Host 物化 bounded WorkRecord（`includeOpening=false`）投影给 caller。**无**独立 `return` 工具、**无** return 投影写入口（AGENT-024；EXEC-028/031）。
6. Inquiry 工具集为 `{ inspect, sphinx MCP }`（AGENT-025、AGENT-030）。装配不得把 `read`/`glob`/`grep` 或其它
   filesystem / `run` / `fork` / `commission` / 终端 / `join` / `horizon` / stealth-browser MCP 面写回 Inquiry。
7. `ToolPermission.Network` → Host schema 键 `stealth-browser-mcp_*`（AGENT-026）。仅 Browser allow。
8. `ToolPermission.Sphinx` → Host schema 键 `sphinx_*`（AGENT-030）。仅 Inquiry allow。

---

## stealth-browser MCP 装配（AGENT-026）

1. `applyOwnedFields` 入口调用 `StealthBrowserMcpConfig.apply`。Ok / Error 都走该函数，fail-closed。
2. 启动判定纯函数：`DISABLED` → disabled；`FIXTURE` 非空 → `node <fixture>`；`WANXIANGSHU_TEST` → disabled；否则 `uvx … @{ref}`。
3. 只覆盖 `config.mcp.stealth-browser-mcp`；其它 MCP 条目不动。
4. 不注册 ToolRegistry spec，不生成 `js-*` 成员。

---

## 内部 Semble MCP（AGENT-027）

1. Kernel 纯函数：`uvxCommand` / `fixtureCommand` / `Launch` / `launchFrom`。
2. Client：Disabled → `[]`；Fixture / Uvx → stdio `initialize` + `tools/call search` → `parseToolResult`。
3. 不调用 `StealthBrowserMcpConfig` / `applyOwnedFields` / `StrengthSpeculate`。
4. 不注册 ToolRegistry spec，不生成 `js-*` 成员，不写 `config.mcp.semble`。

---

## Sphinx MCP 装配（AGENT-030）

1. `applyOwnedFields` 入口调用 `SphinxMcpConfig.apply`。Ok / Error 都走该函数，fail-closed。
2. 启动判定：`SPHINX_MCP_DISABLED` → disabled；`SPHINX_MCP_FIXTURE` 非空 → `node <fixture>`；
   `WANXIANGSHU_TEST` 且无 fixture → disabled；否则 `node <packageRoot>/dist/sphinx/mcp-server.js`。
3. 只覆盖 `config.mcp.sphinx`；其它 MCP 条目不动。
4. 不注册 ToolRegistry spec，不生成 `js-*` 成员。

---

## 可见性

1. Blogger / Distiller / Bookkeeper（含 `fast|deep-bookkeeper`）从一切 provider-visible enum/schema 剔除（AGENT-008）。
2. 示踪面可见集合按 AGENT-009 **静态**构造，不从运行时「当前有哪些 agent」反推：
   - Manager `fork` → fast/deep coder, inspector, devops, browser, inquiry
   - Orchestrator `commission` → fast-manager, deep-manager
   - `inspect` → fast-inspector, deep-inspector
   - `establish-behavior` / `repair-behavior` → fast-coder, deep-coder
3. `horizon()` 只暴露在场名册（Byname / TerminalName），不是可创建目录；无 id / status DTO。
4. Catalog / Role DU / permission 矩阵不得再出现 Meditator/Executor/Student/Teacher；旧名只进 legacy reject 集（AGENT-004）。
5. 工具名投影唯一写入口 = CanonicalRole → permission；旧名 `fork-manager`/`list`/`inspector`(工具)/`verdict`/`blog`/`executor`(工具)/`fork-pty`/`edit-qa`/`return` 非法、无 alias（AGENT-007）。

---

## Inquiry prompt discipline

1. Inquiry system prompt（prompt SSOT）吸收原 Student epistemic style：形成理解、反例、委派
   Inspector（经 `inspect`）、证据/推论/不确定性区分、收敛前不草率终止（AGENT-025）。
2. 不装配 LearningState / QA / Compile / MeditatorLearn|Compile / Student 式 final `return`。
3. 事实调查只经 SyncDelegate `inspect` 工具路径；不给 Inquiry 直读仓库工具。
4. 不得把 `meditator` alias 回 Inquiry；终端就是普通 Assistant completion。
