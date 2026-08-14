# HOW — repository-investigation 的实现模型与约束

> 非 normative。描述当前实现如何满足 WHAT；实现可整体替换（`17-repository.md` INDEPENDENT CHANGE：换查询工具/semantic search orientation 而 evidence contract 不变）。

## 模块地图（当前实现）

### 证据合同散文（provider-facing laws）

| 资源 | 内容 |
|---|---|
| `resources/provider/role/inspector/{en,zh-CN}.md` | Evidence Funnel：`fact → cheapest adequate observation → evidence → consequence`；causal read-only（观察不改变被观察的世界）；locatability（只保留让事实再次可定位的证据）；stop 规则（第一个便宜观察足以结束调查就停下）；「一连串机械搜索不是方法」 |
| `resources/provider/tool/inspect/{description,arg-charge,arg-keywords,unavailable,authority-required,needs-charge,incomplete}/**` | inspect 工具：见证者定位（witness, not second pair of editing hands）；read-only in the causal sense；不做 mutation / 不运行应用制造行为证据 |
| `resources/provider/tool/query-shell/{description,arg-command,missing-command}/**` | 静态 shell 查询：observation not execution；Inspector-only；正例 `git status`/`git diff`/`git log`/`git blame`/`stat`/`wc`；负例 build/test/lint/typecheck/application startup/migration/generation |

### 取证执行

| 文件 | 内容 |
|---|---|
| `src/Wanxiangshu/Infrastructure/OpenCode/Tools/InspectorTool.fs` | `inspect` spec（`Path.*` 资源路径常量）：SyncDelegate → Inspector + `RepositoryWarmStart.prepare`（keywords → 低信任 envelope）；WorkRecord 是 witness evidence，不是 mutation |
| `src/Wanxiangshu/Infrastructure/OpenCode/Tools/FetchTool.fs` | `fetch(shelfmark)`：shelfmark → replay → Fresh/Refreshed/Stale consequence（Casebook 复用；交叉 `knowledge-reuse`） |
| `src/Wanxiangshu/Infrastructure/OpenCode/Tools/FileMutationTools.fs` | `mv`/`rm`（**不**属于 Inspector；仅 Coder 变换面，交叉 `repository-programming`） |

### warm-start 管线

| 文件 | 内容 |
|---|---|
| `src/Wanxiangshu/Domain/RepositoryWarmStartPrompt.fs` | `RepositoryWarmStartHint` / `RepositoryWarmStartSearch`（neutral DTO，不泄漏 Semble infrastructure 类型）；`MaxKeywords=8` / `TopKPerKeyword=4` / `MaxHintsTotal=24` / `MaxWarmStartBytes=64 KiB`；`isDirectConsumer`（Coder/Inspector/DevOps）；`normalizeKeywords`（LF 分行/trim/删空/稳定 exact dedupe/前 8）；`stableDedupeHints`（FilePath+StartLine+EndLine+Content）；`render` / `appendToProviderPrompt`（只删完整 hint entry，超限 fail-open 回 charge/base） |
| `src/Wanxiangshu/Infrastructure/RepositoryWarmStart.fs` | `prepareWithSearch` / `appendToBaseWithSearch`：`Parallel.mapBounded` 并行 wave → 按 keyword ordinal 排序恢复确定性 → fail-open（单 query 失败返回 `[]`）；`collectWithSearch`：零 keywords → `Ok None`（caller 保留 base 字节不变）；非直接消费者 → Error；`workspaceDirectory` 缺失/不存在 → `Ok None` |
| `src/Wanxiangshu/Kernel/SembleMcp.fs` | `Hit`（FilePath/StartLine/EndLine/Content/Score/TotalLines）+ `serverName`/`toolName`/launch 常量 |
| `src/Wanxiangshu/Infrastructure/SembleSearchCodec.fs` | `parseText` / `parseToolResult`（MCP payload → Hit list；残缺条目丢弃） |
| `src/Wanxiangshu/Infrastructure/SembleMcpClient.fs` | `launchFromVars`（Disabled/Fixture/Uvx 启动判定）+ `search`（stdio JSON-RPC） |
| `src/Wanxiangshu/Infrastructure/SembleMcpStdio.fs` | stdio MCP transport |

### 关键约束（实现即合同）

```text
Semble 不注入 Host config.mcp / 不进 permission schema / 不进 ToolRegistry / 不生成 js-* 成员
RepositoryWarmStart 不写 Casebook、不伪造 read/grep/tool history
repoPath 只用真实 WorkspaceDirectory；缺失跳过，禁猜 "."
provider envelope：charge = instruction/assignment；repository_search/repository_hint = data
```

## 依赖（DEPENDS ON，逐条理由）

| 依赖 | 理由 |
|---|---|
| `office-capability` | 谁能取证由 office consequence 决定（Inspector 的只读权限集、Inquiry 只能 inspect/sphinx、`inspect` 不泄露 query-shell 取证权）；本包消费「Inspector = evidence acquisition」的 office 定位。 |
| `participant-horizon` | provider 只看到能改变合法行动的最小事实（low-trust hints 明确标注、不泄漏 Semble infrastructure 类型、index 不泄漏机器字段）——信息准入边界。 |

## 历史与弃权

### 被拒方案（详见 `archive/changes/completed/repository-warm-start.md`、`archive/docs/why/agent.md`、`archive/docs/why/casebook.md`）

把 Semble 注册成 Host MCP / provider tool / ToolPermission；Strength Replica 工具面加入 Semble；自动从 charge 抽词 / tokenizer / noun picker / LLM generator；cross-call warm-start cache；warm-start 注入 provider-visible `read`（假工具历史）；把 hints 直接写入 Casebook；搜索零命中 → 告知「确认不存在」；猜 `repoPath = "."`；非直接消费者接收 snippets。均记录于 `WHY.md` §历史拒绝方案。

### 判定为 HOW（非 normative；不入 WHAT）

- Semble 启动判定（`SEMBLE_MCP_*` 环境变量、`uvx --from "semble[mcp] @ git+...@{ref}"`、fixture 命令、`WANXIANGSHU_TEST` 行为）、MCP transport 细节 → Host adapter 机制（`host-boundary` 交叉）。
- `MaxKeywords=8` / `TopKPerKeyword=4` / `MaxHintsTotal=24` / `MaxWarmStartBytes=64 KiB` → tuning 常数（HANDOFF §12）。
- 当前 Inspector Persona（Scout/Investigator）、`read`/`glob`/`grep`/`query-shell` 工具名、`inspect` 工具的参数 schema → 当前实现词汇。
- Inquiry 的 Sphinx 集成（A*/Bayes/MCTS）→ `epistemic-reasoning`。

### 判定为 GARBAGE（migration/clean-break 沉积）

- 历史「Semble 注入 provider-visible read」的 injection 已废止（AGENT-027 禁令）——absence 由门禁保证，不另立命题。
- `inspect` 旧名/旧 schema（如有）的兼容路径不迁移。

### 不归本包（COVERAGE 交叉确认）

- Casebook fetch/replay/freshness/LRU/lifecycle → `knowledge-reuse`（含 `fetch` 工具面）。
- 外部 web/public-web provenance → `external-investigation`（Browser）。
- repository mutation / execution → `repository-programming` / `process-execution`。
- 谁能携带 keywords 的 invocation DAG → `delegation`。
