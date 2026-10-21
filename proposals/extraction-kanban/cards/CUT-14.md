# CUT-14　失败预算、恢复决议与结算

返回 [BOARD](../BOARD.md) · [CARDS](../CARDS.md#cut-14)

## 1. 当前状态

- **列**：`DONE`（eng-cut14c-goose，接手 eng-cut14b-goose、eng-cut10g-goose）。
- **前置**：CUT-10、CUT-11、CUT-12 均 `DONE`。

## 2. 子范围

| # | 规则 | 新 owner | 主仓提交 | 证据 |
|---|---|---|---|---|
| ① | 一次精确执行失败的唯一决议：十一类失败 × 四个持久阶段 → 五路结论（保留/等待对账/重试/终结于 provider 前/终结于 provider 后）+ breaker 迁移 + 精确 fence 保留/释放 + 结算后致命；重试授权绑定（logical run, provider run, request kind） | wanxiangli/failure 0.49.0（wanxiangli 774e38c） | 见本卡提交 | [diff-failure.txt](../evidence/CUT-14/diff-failure.txt)、[mutants](../evidence/CUT-14/diff-failure-mutants.txt)、[touched tests](../evidence/CUT-14/failure-touched-tests.txt) |
| ② | Logical Run 的 provider 失败预算：精确 attempt 去重（32 窗口）、连续计数单次推进、耗尽终态及其判定顺序、业务成功清零、已计失败的重放应答（仅最新精确 attempt 可再授权） | wanxiangli/failure-budget 0.54.0（wanxiangli 342de4d） | 见本卡提交 | [diff-budget](../evidence/CUT-14/diff-budget.txt)、[mutants](../evidence/CUT-14/diff-budget-mutants.txt)、[diff-host](../evidence/CUT-14/diff-host.txt)、[diff-replay](../evidence/CUT-14/diff-replay.txt)、[replay mutants](../evidence/CUT-14/diff-replay-mutants.txt)、[touched tests](../evidence/CUT-14/budget-touched-tests.txt) |
| ③ | 真实失败后的下一 attempt（同 provider 原样重试 → 再败判死后重试） | RETAINED 宿主 `ProviderAttemptRecovery.next`（已是唯一 owner） | — | 见 §5 |
| ④ | Host 内建 retry 边界核实 | 核实，不改代码；GAP-143/GAP-144 保留 | — | 见 §6 |

## 3. ① 验收（§9）

- 差分对象：旧的宿主决议按原样编译运行。被比较的是切换前构建出的 `dist/Execution/Failure/Surface.js`，从切换前 dist 快照里直接调用，不是人工转写。新的一侧是 wanxiangli/failure 的输出，映射成同一视图后比较。
- 差分结果：3 个种子 × 200,000 个输入，0 处不同，异常与否也一并比较。输入里约 17% 是畸形的，比如未知失败类、缺 commitment、坏的 phase 或 provider 字段。五路结论都出现了，RetryFreshAttempt 约 1,400 次/种子。
- 变异：7 个种子变异全部被抓住（430–4,787 处不同）。
- 库：`src/failure/tests` 5/5 通过；SPEC[build-001] failure 在没有任何兄弟源码时也能编译并跑通自身测试；SPEC[build-008] exports/files 一致性通过。
- 宿主：`Execution/Failure/Policy.fs` 删掉了手写的 deriveBreaker、deriveCapacitySettlement、deriveFatality、terminalResolution、retryProviderResolution 和 release/retainCapacity。剩下的只是把 F# 类型映射成库的 plain 输入，再把决议映射回来。执行键和精确 fence 按引用往返；重试授权由宿主用自己的强类型 facts 加上库给的 decisionId 重建，不从字符串反解身份。`execution-failure-policy-002` 新增断言：Policy.fs 必须 import `wanxiangli/failure`，旧的派生函数名不许再出现。
- 公共签名 `Policy.fsi` 不变；九个调用点（Retry、ProviderFailureSurface、RecoverySurface、RecoveryRuntimeSurface、SessionRecoveryHost、ProviderFailurePresentation、ChatExecutionJournal、PluginHostInterop、Surface）一行都没改。
- 净改进：决议规则原先在宿主手写（Policy.fs 159 行），现在只有库一个 owner，并有独立测试、隔离构建和变异证据。宿主 Policy.fs 改成约 170 行的类型映射，行数基本持平；收益在于 owner 唯一，而且可以独立验证。
- 回退：revert 本卡主仓提交即可。库 0.49.0 只新增了 `./failure` 入口，留在 main 不影响旧 master。

## 4. ② 验收（§9）

- 差分对象：旧宿主的预算与投影按原样编译运行，即切换前 dist 快照里的 `Fallback/ProviderFailureSurface.js` 的 `budget` / `providerFailureProjection`，不做人工转写。
- 库差分（[diff-budget.mjs](../evidence/CUT-14/diff-budget.mjs)）：旧宿主 vs wanxiangli/failure-budget，3 个种子 × 5,000 条随机操作序列，共约 605,000 步，0 处不同。去重键格式逐次比较。所有拒绝类（AlreadyExhausted/DifferentRun/AlreadyObserved/InvalidTransition）、耗尽、成功清零、mayRetry 真/假和 verdict 都出现了。7 个种子变异全部被抓住：去重失效、窗口 31、跳步允许、耗尽与重复的判定顺序、成功不清键、verdict 用 >、忽略 authority root。
- 宿主端到端差分（[diff-host.mjs](../evidence/CUT-14/diff-host.mjs)）：同样的操作序列，同时驱动切换前和切换后的已编译宿主 surface，3 个种子约 605,000 步，0 处不同。
- 重放差分（[diff-replay.mjs](../evidence/CUT-14/diff-replay.mjs)）：比较 `ProviderFailureLedger.replayLatestFailure` 切换前后的编译产物。这是私有函数，做法是在原文件旁写一份 probe 副本，只追加一行 export，其余不动。3 个种子共约 909,000 次重放，0 处不同，RetryAuthorized、RetryExhausted、EpisodeSuperseded 三类都出现了。3 个变异全部被抓住：任一已记键都可重放、忽略预算、耗尽仍授权。
- 测试：受影响的 59 个测试文件 236 通过、0 失败（比 ① 多 1 条，是新增的 execution-failure-policy-002 owner 断言）。4 skipped / 35 todo 和 ① 相同。`architecture.mjs` OK。库 `src/failure-budget/tests` 加 `src/failure/tests` 共 9/9 通过。
- 宿主：`Fallback/Projection.fs` 删掉了 DedupeWindow、remember/truncate、手写判定链和 budgetMayRetry。`Ledger.fs` 删掉了 exactLatest 的手写 tryHead 比较。`FailureBudget.fs` 删掉了去重键的 `String.Join` 和 `>=` 判定。剩下的只是把 F# 投影和库的 plain 状态互相映射，run 身份原样带回。`execution-failure-policy-002` 新增断言：Projection.fs 必须 import `wanxiangli/failure-budget`，旧手写名不许再出现，宿主 `DefaultBudget` 等于库的 `defaultBudget`。
- 余留：`ProviderFailureBudget.recordFailure/recordSuccess/isValidRecord` 只供 JS 测试 surface 使用（context-compression-005/006/023、provider-attempt-recovery-007），生产路径不用；原样保留，没有再接到库上。
- 公共签名：`Projection.fsi` 新增 `replay` 和 `ProviderFailureReplay`，`FailureBudget.fsi` 新增 `view`，其余不变。
- 回退：revert 本卡主仓提交即可。库 0.54.0 只新增了 `./failure-budget` 入口。

## 5. ③ 裁决：RETAINED

`ProviderAttemptRecovery.next`（`RetryPolicy.fs`，2 行布尔）已经是"真实失败后下一 attempt"的唯一 owner。它的两个调用者，`Fallback/Workflow.fs` 的 `settleFailedAttemptTarget` 和测试 surface 的 `Execution/Failure/Surface.recoveryStep`，都只消费它的结论，没有竞争决议。输入事实 `failureIsSameProviderRetry` 读宿主 journal（continuation kind + ProviderStarted 的 provider run），执行动作 `ModelRouting.retain/condemn` 是宿主副作用，两者按 CARDS"物理 retry 由外层执行"本就留在宿主。`Fallback/Retry.fs` 只把 ① 的决议（wanxiangli/failure）、② 的预算（wanxiangli/failure-budget）和 Admit/Redispatch 端口组合起来，自己不做判断。为一个布尔跨 JS 边界开库，不是 §9 意义上的净改进，所以保留在宿主。provider-attempt-recovery-021、execution-failure-policy-002 已断言这条规律。

## 6. ④ 核实：Host 内建 retry 边界

- `OpenCode/Host/ManagedAgentConfig.fs` 仍把 `experimental.chatMaxRetries` 固定写成 0，host-provider-failure-ownership-001 通过（实际 config hook 覆盖既有值和旧环境变量）。
- host-provider-failure-ownership-005 的 retry-owner 启发式通过：① 和 ② 之后，retry、fallback 的决议只来自 wanxiangli/failure 和 failure-budget，宿主侧没有第二个 writer。003、006 的分类器断言和 007 的版本钉也都通过。
- 002 是安装版 Host 的集成实测，在 scoped runner 里是 skip，本机也没有安装 OpenCode 可执行文件，所以不计通过。GAP-144（1.18.29 在配置为 0 时仍重试）和 GAP-143（整链缺口）照旧作为可执行 TODO 保留，本卡不关闭，也不声称宿主已修好。

## 7. 退出条件

- 相同决议不再由 Failure、RetryPolicy、Host presentation 多处竞争：执行失败决议只在 failure，预算、去重、重放只在 failure-budget，下一 attempt 只在 `ProviderAttemptRecovery.next`。
- 旧分类的等价性由已编译的旧实现差分证明，不是只改枚举名。历史 Host 自动重试缺口保留。
