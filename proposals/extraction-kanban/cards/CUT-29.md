# CUT-29：Strength / 投机调查去向 —— 裁决

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-29)。

- **裁决人**：eng-cut09g-goose，2026-10-21，按仓主“完全自主决定”的授权。
- **裁决**：逐项裁决，不一揽子造“智能调查引擎”。Strength/投机调查已由仓主删除（REMOVED，不记为抽取成功），本卡清掉死残留；Warm-start 纯规则抽取；Semble MCP 传输与只读调查契约留主仓（RETAINED）。

## 依据（亲眼核对，master d57f886c5）

| 部件 | 现状 | 真实消费者 | 裁决 |
|---|---|---|---|
| 估计、预算、Replica、Strength 委派链（`src/Wanxiangshu/Strength/*`、`PluginStrengthPorts`、`StrengthSurface`）与 `requirements/speculative-investigation` | 已由仓主 `6eef89d1e`（retire … Replica）整目录删除 | 无 | **REMOVED**（仓主已删，非本卡抽取）。死残留：`ToolRegistry.Path.DeniedStrength` 与 `resources/provider/tool/registry/denied-strength/*` 无任何读取者 → 本卡删除 |
| 只读调查契约（repository-investigation WHAT 001–005） | 角色权限 + 提示约束，无运行时算法 owner | 角色/工具注册 | **RETAINED**：是产品规则与权限表，不是可抽算法；继续由 WHAT 与现有测试守护 |
| Warm-start 纯规则（WHAT 007/009：关键词规范化、每词 top-k、稳定去重、总条数上限、整条删减到字节预算、省略计数；WHAT 008 直接消费者角色） | `Repository/Investigation/WarmStart/Prompt.fs` 手写 | `Fork/OpenCode/Tool.fs` 经 `RepositoryWarmStart.appendToBaseDocument` | **抽取**（子范围①）→ `wanxiangli/warm-start`；宿主只留 Semble 调用、LlmFacing 渲染与语言资源 |
| Semble MCP 客户端（`Semble/{Client,Mcp,Stdio,SearchCodec}`） | 进程/stdio 传输与外部工具输出解码 | Warm-start | **RETAINED**：宿主 I/O 与外部协议适配，fail-open 已有测试（002/009） |

- 未裁决消费者：CUT-13（委派）尚未 DONE；子范围①不改 `RepositoryWarmStart` 对 Fork 的公开签名，写集合与 CUT-13 不相交。整卡 DONE 待 CUT-13 核对 Fork 接线后确认。

## 子范围

1. **Warm-start 选择与预算律**（已合入，§1）：`normalizeKeywords`、命中→提示条目的选择（每词 top-k、按关键词序 + 局部名次、`(path,start,end,content)` 稳定去重、总数 24 上限、省略计数）、整条删减到 64 KiB（宿主提供量度函数）抽入 `wanxiangli/warm-start`。反例：大小写不同的关键词被并掉；空白行触发搜索；超限时截断半条；重复命中挤掉别的关键词；省略计数漏算去重项。
2. **死残留**：删除 `DeniedStrength` 键与两份资源（已合入，§1）。

## 不做

- 不恢复任何 Strength/Replica 功能；不改 Semble 启动与环境变量；不改提示资源文本。

## §1 子范围① Warm-start 律交给 `wanxiangli/warm-start`（eng-cut09g-goose，2026-10-21）

- **库**：`WarmStartLaw`（库提交 `bd6f41c`，随 `b792c70` 发布 0.51.0）：`normalizeKeywords`、`rankHits`、`select`（按关键词序稳定合并、`(filePath,startLine,endLine,content)` 稳定去重、24 上限、omitted 计数）、`fit`（整条删尾到调用方量得的字节 ≤ 64 KiB，零条仍超限返回 null）、`isDirectConsumer`（规范标签 `engineer`/`devops`）。库测试 `src/warm-start/tests/001.test.mjs`（SPEC 1–5）。
- **主仓**：`WarmStart/Prompt.fs` 删手写关键词收集、去重、截断、预算递归与角色判定，只留 TOML 渲染与字节量度；`Runtime.fs` 删每词 top-k 与排序（改用 `rankHits`/`select`）；无外部消费者的 `stableDedupeHints` 删除。`RepositoryWarmStart` 对 Fork 的公开签名不变。
- **死残留**：`ToolRegistry.Path.DeniedStrength` 与 `resources/provider/tool/registry/denied-strength/{en,zh-CN}.md` 删除（无读取者，Replica 已由 6eef89d1e 退役）。
- **差分**（`evidence/CUT-29/differential.{mjs,out}`）：同种子输入分别驱动 base dist（master d57f886c5）与候选 dist 的 `WarmStartSurface`。20000 轮：关键词 20000、render 20000、append 20000、prepare/appendToBase（含失败分片、无工作区、非直接消费者）5000，**sha256 全等**；覆盖：15133 次出现 omitted、15090 次因字节预算整条删减、558 次头部超限回退。**唯一差异**：测试面直接传入**未按序**的 searches 时，候选按 ordinal 排序（律的一部分），base 按传入序；候选未排序输出与 base 预先排序后的输出 sha256 全等（`unsorted` vs `unsortedPreSorted`）。生产路径 `RepositoryWarmStart` 一直先排序再渲染，故生产行为不变。
- **变异**（`evidence/CUT-29/mutants.txt`）：8 个变异（大小写不敏感去重、top-k 5、去重忽略 content、omitted 漏计删减、select 不排序、manager 成直接消费者、omitted 漏计重复、预算 `<` 代替 `<=`）全部被库测试抓住。
- **回归**（`evidence/CUT-29/suites.out`）：repository-investigation 12/0、capability-enforcement 133/0、participant-horizon 35/0、provider-projection 76/0、provider-language 25/0、action-affordance 2/0 与 base 相同；delegation 63 pass / 23 fail / 33 cancelled 与 base 完全相同，失败用例集合逐条一致（既有，非本卡）。`node scripts/checks/architecture.mjs` OK。
- **回退**：纯计算、不改事件与资源文本（只删无读取者的资源）；`git revert` 主仓提交即回到手写版本（库 0.51.0 可保留）。
- **未做**：全量 `scripts/verify.mjs`、integration tier、fantomas（本机无 fantomas）。

## §2 关闭（eng-cut09i-goose，2026-10-21）

- CUT-13 已在看板 DONE（b1b59d6f4）。亲眼核对：`RepositoryWarmStart` 的生产消费者只有 `Execution/Delegation/Fork/OpenCode/Tool.fs`（`appendToBaseDocument`）以及插件装配处的 `open`；CUT-13 的收尾提交没有改这条接线，本卡也没有改它的公开签名，§1 的“整卡 DONE 待 CUT-13 核对 Fork 接线”因此满足。
- 退出条件（CARDS CUT-29）：抽出的算法（warm-start 律）只有 wanxiangli 一个 owner；不搬的部分已逐项登记——Strength/Replica 等 REMOVED（仓主已删，非本卡抽取）、只读调查契约与 Semble 传输 RETAINED。没有长期先验、历史迁移或工具配置被删除。
- 落地复验：落地循环 `logs-cut09i/loop29*.out` 在推送前检查中误报 `GITLINK_OLDER`（脚本拿已前移的 origin/master gitlink 比较，不是本卡问题）后退出；接手者 eng-cut09j-goose 用修正后的循环 `logs-cut09j/loop29.out` 重来：rebase 到最新 master 后重新构建，repository-investigation、provider-language、capability-enforcement、provider-projection 四个套件合计 283 tests / 246 pass / 0 fail / 0 cancelled / 12 skip / 25 todo（在 `507d68d01` 上 `--clean` 全量构建；之后 master 若只做不相交前移，则只 rebase 并重跑推送前检查）；推送前跑了 lockfile、gitlink 祖先关系和 `architecture.mjs` 检查。
- **状态**：CUT-29 `DONE`（两刀落地为 master `01488ebe4` 及其父提交）。
