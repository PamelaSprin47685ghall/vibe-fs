# Agent — 目标实现

## Implements

行为合同见 `what/agent.md`；本文件只描述目录装配、权限计算和可见面生成算法。

## Ownership

模块边界与写入口见 `shape/agent.md`。

---

## 启动装配

1. 枚举 AGENT-002 **二十名**；缺一 fail fast。不得装入 `fast|deep-student|teacher`。
2. 校验 `peer(fast)=deep` 对称；model 字符串非空且 pair 内互异。  
3. 非法旧名（AGENT-004，含 `student`/`teacher`）在配置验证阶段拒绝，无 alias。
4. 静态校验 SyncDelegate DAG（AGENT-024）无环：仅允许
   `Meditator|Coder|DevOps → Inspector` 与 `DevOps → Coder`。
5. SyncDelegate tier：owner effective tier 确定性映射到 delegate tier；配置不得提供 per-call 覆盖。

---

## 权限对象

1. 由 `CanonicalRole` 导出角色工具集 → Host-final permission + ToolRegistry 两层（AGENT-007）。  
2. Role 未定 → 模型可见插件工具集为空。  
3. `external_directory="allow"` 仅经 `StaticTools.permissionObj` → `ManagedAgentConfig.applyOwnedFields` 写入每个 managed agent（AGENT-019）。  
4. 禁止全局 permission 顶替 agent 级写入。
5. `InvocationMode = SynchronousDelegate` 时在基线工具集上投影 `return`（profile，非 PC）；普通
   WorkMain 调用不得因此常驻 `return`。
6. Meditator 工具集仅为 `{ inspector }`（AGENT-025）。装配不得把 `read`/`glob`/`grep` 或其它
   filesystem / executor / fork / stealth-browser MCP 面写回 Meditator。
7. `ToolPermission.Network` → Host schema 键 `stealth-browser-mcp_*`（AGENT-026）。仅 Browser allow。

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

## 可见性

1. Blogger/Executor 从一切 provider-visible enum/schema 剔除（AGENT-008）。
2. fork schema 可见集合按 AGENT-009 **静态**构造，不从运行时「当前有哪些 agent」反推。  
3. list() 只暴露 running handle，不是可创建目录。
4. Catalog / Role DU / permission 矩阵不得再出现 Student/Teacher；旧名只进 legacy reject 集（AGENT-004）。

---

## Meditator prompt discipline

1. Meditator system prompt（prompt SSOT）吸收原 Student epistemic style：形成理解、反例、委派
   Inspector、证据/推论/不确定性区分、收敛前不草率终止（AGENT-025）。
2. 不装配 LearningState / QA / Compile / MeditatorLearn|Compile / Student 式 final `return`。
3. 事实调查只经 SyncDelegate `inspector` 工具路径；不给 Meditator 直读仓库工具。
