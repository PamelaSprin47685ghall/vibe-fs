# repository-investigation

## 一句话 WHY

> repository claim 必须由可定位、可追溯的真实 observation 建立；reasoning、semantic search hint、旧 Case、外部 web 都不能自动冒充当前 repository evidence。调查是证据采集，不是思考——思考决定问什么，观察才产生事实。

## 阅读顺序

1. `WHY.md` — 为什么这个包必须独立存在；RED 长什么样。
2. `WHAT.md` — 唯一 normative 合同：9 条编号命题（`REPOSITORY-INVESTIGATION-001..009`）。
3. `HOW.md` — 实现模型（Inspector / RepositoryWarmStart / SembleMcp）与约束；含历史与弃权。
4. `PROOF.md` — 每条命题的可执行落点表；SPLIT@cutover 计划；semantic anchor 归属。

## WHAT 概览

| ID | 命题（压缩） |
|---|---|
| `REPOSITORY-INVESTIGATION-001` | repository claim 必须由真实观察建立：推理/旧缓存/搜索 hint/外部 web 都不能自动成为当前 evidence。 |
| `REPOSITORY-INVESTIGATION-002` | observation 可定位、可追溯（locatability + provenance：path/行/内容）。 |
| `REPOSITORY-INVESTIGATION-003` | evidence acquisition 与 semantic reasoning 分层：reasoning 决定问什么，不增加 evidence。 |
| `REPOSITORY-INVESTIGATION-004` | 选 cheapest adequate observation，足够回答当前事实问题时停止。 |
| `REPOSITORY-INVESTIGATION-005` | observation 因果只读：不为观察改变 repository、不运行应用制造新行为。 |
| `REPOSITORY-INVESTIGATION-006` | warm-start/semantic search 命中 = 低信任 orientation，必须真实观察确认后才成为 fact。 |
| `REPOSITORY-INVESTIGATION-007` | explicit keywords 每次 fresh search；不自动抽词、无 cross-call cache；无 keywords 零工作。 |
| `REPOSITORY-INVESTIGATION-008` | keywords 只对直接消费者（Coder/Inspector/DevOps）可用；repoPath 只用真实 WorkspaceDirectory。 |
| `REPOSITORY-INVESTIGATION-009` | warm-start 有界且确定：并行 wave、稳定 dedupe、超限只删完整 entry、绝不截断 TOML 字符串。 |

## HOW 概览

- **证据合同散文**：`resources/provider/role/inspector/{en,zh-CN}.md`（Evidence Funnel / causal read-only / locatability / stop 规则）；`resources/provider/tool/{inspect,query-shell}/**`（inspect = 见证者、query-shell = observation not execution）。
- **取证执行**：`src/Wanxiangshu/Infrastructure/OpenCode/Tools/InspectorTool.fs`（inspect spec，SyncDelegate → Inspector + warm-start keywords）；`FetchTool.fs`（fetch = Casebook 复用，交叉 `knowledge-reuse`）。
- **warm-start**：`src/Wanxiangshu/Domain/RepositoryWarmStartPrompt.fs`（normalizeKeywords / render / bounds）；`src/Wanxiangshu/Infrastructure/RepositoryWarmStart.fs`（parallel wave / fail-open / neutral DTO）。
- **Semble 适配**：`src/Wanxiangshu/Kernel/SembleMcp.fs`（Hit 类型）；`src/Wanxiangshu/Infrastructure/{SembleSearchCodec,SembleMcpStdio,SembleMcpClient}.fs`（stdio MCP 客户端）；不注入 Host mcp / permission / ToolRegistry。
- 细节见 `HOW.md`。

## proof 概览

- 本包测试：`repository-warm-start.test.mjs` + `semble-mcp.test.mjs`（MOVE 自 `tests/unit/agent/`）+ `investigation-resource-laws.test.mjs`（NEW，双语资源律锚点）。
- 交叉 REUSE：`fetch-tool.test.mjs`（knowledge-reuse 的「fetch 不写 subject」）、`scripts/checks/semantic-anchors.mjs`（inspector 组锚点）。
- 单跑：`WANXIANGSHU_PROVIDER_LANGUAGE=en node --test requirements/repository-investigation/tests/<file>`。全套：`node tests/unit/run.mjs`。

## 边界（DOES NOT OWN）

- Office authority canonical definition → `office-capability`；「谁能取证」（Inquiry 工具面、Inspector 权限集）→ `capability-enforcement`/`office-capability`。
- Casebook cache（fetch/replay/freshness）→ `knowledge-reuse`；外部/public-web facts → `external-investigation`；repository mutation/execution → `repository-programming`/`process-execution`。
- 当前 Inspector Persona（Scout/Investigator）、Semble MCP 的 uvx/ref/env 启动判定、`read`/`glob`/`grep` 工具名 → HOW（当前实现词汇）。

## DEPENDS ON

`office-capability`、`participant-horizon`（逐条理由见 `HOW.md` §依赖）。
