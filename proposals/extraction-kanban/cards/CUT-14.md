# CUT-14　失败预算、恢复决议与结算

返回 [BOARD](../BOARD.md) · [CARDS](../CARDS.md#cut-14)

## 1. 当前状态

- **列**：`DOING`（eng-cut14b-goose，接手 eng-cut10g-goose）。
- **前置**：CUT-10 `DONE`、CUT-11 `DONE`；CUT-12 `DOING`。子范围①与 CUT-12 写集合不相交（只动 `Execution/Failure/Policy.fs`），先做；整卡 DONE 待 CUT-12。

## 2. 已合入的子范围

| # | 规则 | 新 owner | 主仓提交 | 证据 |
|---|---|---|---|---|
| ① | 一次精确执行失败的唯一决议：十一类失败 × 四个持久阶段 → 五路结论（保留/等待对账/重试/终结于 provider 前/终结于 provider 后）+ breaker 迁移 + 精确 fence 保留/释放 + 结算后致命；重试授权绑定（logical run, provider run, request kind） | wanxiangli/failure 0.49.0（wanxiangli 774e38c） | 见本卡提交 | [diff-failure.txt](../evidence/CUT-14/diff-failure.txt)、[mutants](../evidence/CUT-14/diff-failure-mutants.txt)、[touched tests](../evidence/CUT-14/failure-touched-tests.txt) |

## 3. ① 验收（§9）

- 差分对象：旧的宿主决议按原样编译运行。被比较的是切换前构建出的 `dist/Execution/Failure/Surface.js`，从切换前 dist 快照里直接调用，不是人工转写。新的一侧是 wanxiangli/failure 的输出，映射成同一视图后比较。
- 差分结果：3 个种子 × 200,000 个输入，0 处不同，异常与否也一并比较。输入里约 17% 是畸形的，比如未知失败类、缺 commitment、坏的 phase 或 provider 字段。五路结论都出现了，RetryFreshAttempt 约 1,400 次/种子。
- 变异：7 个种子变异全部被抓住（430–4,787 处不同）。
- 库：`src/failure/tests` 5/5 通过；SPEC[build-001] failure 在没有任何兄弟源码时也能编译并跑通自身测试；SPEC[build-008] exports/files 一致性通过。
- 宿主：`Execution/Failure/Policy.fs` 删掉了手写的 deriveBreaker、deriveCapacitySettlement、deriveFatality、terminalResolution、retryProviderResolution 和 release/retainCapacity。剩下的只是把 F# 类型映射成库的 plain 输入，再把决议映射回来。执行键和精确 fence 按引用往返；重试授权由宿主用自己的强类型 facts 加上库给的 decisionId 重建，不从字符串反解身份。`execution-failure-policy-002` 新增断言：Policy.fs 必须 import `wanxiangli/failure`，旧的派生函数名不许再出现。
- 公共签名 `Policy.fsi` 不变；九个调用点（Retry、ProviderFailureSurface、RecoverySurface、RecoveryRuntimeSurface、SessionRecoveryHost、ProviderFailurePresentation、ChatExecutionJournal、PluginHostInterop、Surface）一行都没改。
- 净改进：决议规则原先在宿主手写（Policy.fs 159 行），现在只有库一个 owner，并有独立测试、隔离构建和变异证据。宿主 Policy.fs 改成约 170 行的类型映射，行数基本持平；收益在于 owner 唯一，而且可以独立验证。
- 回退：revert 本卡主仓提交即可。库 0.49.0 只新增了 `./failure` 入口，留在 main 不影响旧 master。

## 4. 余项（本卡仍 DOING）

- ② 预算单次推进与失败去重：`Participant/Provider/Attempt/FailureBudget.fs`（连续失败计数、精确 attempt 去重键、isValidRecord）和 `Fallback/Ledger|Projection|ProviderFailureFactFold`。
- ③ 真实失败后的下一 attempt：`RetryPolicy.fs` 的 `ProviderAttemptRecovery.next`（同 provider 原样重试 → 判死后重试）与 `Fallback/Retry.fs` 的组合。
- ④ Host 内建 retry 边界核实。历史 Host 自动重试缺口照旧保留，不关 GAP。
