# CUT-22 证据

| 项 | 值 |
|---|---|
| 主仓 base | master `5fb955e23`（候选在其上做；旧代码对照用 `13944a5db` 的干净构建，两者之间 Authority/Origin、Model、PairProgrammingThought*、Persistence/Journal 源码相同；Host/Facts 只有 TaskDelivery 栅栏部分不同（`0a742980c`），`PairProgrammingGuidelineAnchored` 与 `ConcernPlacementBatch` 形状相同） |
| 候选 | 本卡提交；wanxiangli gitlink 不变（`351e3c5`，0.28.0） |
| 构建 | `node scripts/build.mjs` exit 0，725 modules linked |

## 1. 回归测试先红后绿

`node --test requirements/concern-routing/tests/007.test.mjs requirements/attention-regulation/tests/005.test.mjs` 在旧代码树上运行：新加的 2 条失败（`not ok … DeferredWorkPresentation`、`not ok … ConcernPlacement residue`），原有 2 条通过。在候选上两包共 15/15 通过。

## 2. 日志兼容

`node journal-compat.mjs <base-root> <cand-root>`：

```
codec cases 150 ok; journal replay base pairs 5 = candidate pairs 5
```

## 3. 套件

| 套件 | 候选 | 与旧 base 比较 |
|---|---|---|
| concern-routing | 8 pass | 新增 1 条 |
| attention-regulation | 7 pass | 新增 1 条 |
| interaction-authority | 104 pass / 13 todo | 同 CUT-09 |
| dispatch-protocol | 99 pass / 1 cancel | 同 |
| crash-reconciliation | 52 pass | 同 |
| prefix-stability | 52 pass / 8 cancel | 失败名集合相同 |
| durable-events | 236 pass | — |
| host-boundary | 225 pass / 2 fail（023、026） | 失败名集合相同 |
| provider-projection | 76 pass | — |
| context-compression | 50 pass | — |
| provider-attempt-recovery | 61 pass | — |
| structured-workflow | 102 pass / 1 fail（005） | 失败名集合相同 |
| capability-enforcement | 133 pass | — |
| degeneration-guard | 55 pass | — |
| participant-identity | 51 pass | — |
