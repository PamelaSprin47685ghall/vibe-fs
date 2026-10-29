# 指南自举交接（2026-10-29 JST）

| 仓 | HEAD | 备注 |
|---|---|---|
| vibe-fs | `86c103a9`（+本 handoff 提交） | 合 #70（`15a7b718`）后清债：剩余独立 resetScope／estimate 用例 skip、proposals 补注、scope-progress 宿主边界注释 |
| wanxiangli | `4a7e98a` | 合 #8（UTF-8 projection bound）；含 `3ea4f68` scope-progress 产品边界 |

## Stash（保留）
- 主仓 `stash@{0}`：PluginTransforms/managed-chat 半成品（已被 5dd99b87 覆盖）
- li `stash@{0}`：failure 半成品（已被 c8d18b9 覆盖）

## 自举仍悬（裁定类勿臆造）
1. 两项设计裁定追认（E1 已 A+A）
2. phase/step ~22 处逐处裁定（档案未收回）
3. 「22 处」清单完备性
4. wx `wanxiangli-ce-replay-wt` 占 main@f1d01a0（勿贸然删）
5. 上游额度曾空转

## 已持久化
- `docs/委托惯例-验证前查总工头合入.md`

## Scope 续收口（本轮）
**已做：** OMP 全工具 scope 注入+校验；removeLast 出站坍缩（OpenCode+OMP）；Advisor 改挂 retention advisorDue（streak≥3 / remaining=0）；冷启 journal 绑定+ensureEpoch 重放；estimate 改为可选；Stall/ResetDue 不再开顾问；steer 文案改 retention；build gen40+。

**过渡已清：** 独立 `resetScope` 工具已物理删除；档案重锚走任意工具 `scope.kind=resetScope` → `ScopeReset.commit`；`remainingScopeTurnsEstimate` schema/驱动力已删；removeLast 投影用 beat 别名（providerRunId/callId）兜底。

**PairHint 屏障：** `latestReset` / `isResetWirePart` / `isAlwaysRetained` / `neutralIsReset` 识别 `scope.kind=resetScope`（含 arguments/state.input 与打平字段），过渡仍兼容旧工具名；带 reset 注解的调用及其结果保留。宿主中立投影透传 `scope`。wx `ce-replay-wt` / 裁定仍不臆造。

## docs-li / docs-shu（本轮）
- li：capability／pair-hint／scope-retention／scope-progress AGENTS 对齐注解入口；估计路径标退役。
- shu：共同法／scope-protocol／js-program·bookkeeper 资源去掉 estimate 必填；功能导览、结对指南、fork-child 收尾口径；proposals 两份 Scope 稿打历史条；requirements README 钉现行 Scope 一句。
- **compat-pass（本轮）**：优先 pack WHAT／WHY 加 COMPAT 条；context-compression [033]/[034]/[045] 改注解+retention；scope-progress 测试标 PACKAGE-HISTORICAL；omp-scope-seam 估计／独立 resetScope 注册用例 `test.skip`；049／064 改断言无 estimate；arg-estimate 资源保留退役说明；PairHint 注释注解优先。
- **#70 / #8（本轮）**：[wanxiangli#8] 已合，gitlink 钉 `4a7e98a`；[vibe-fs#70] 本地 merge 进 master `15a7b718`（冲突按现行口径：独立 resetScope 保持删除、estimate 夹具继续剥、managed-chat 保留 master 修）。
- **清债（本轮，`74097a8a`/`9258fc26`/`86c103a9`）**：
  - 残留 estimate 夹具：advisor 007／progress-recovery 007／workspace-isolation 007 带键用例均已 `test.skip`（仅确认）；scope-hni-physical 052、051 与 omp-negotiate-physical scope-progress-018 → skip [PRODUCT-RETIRED estimate]；work／delegation／repo-programming 已无正向传键（049／064 负向断言保留）。
  - 首次强 steer 断言（scope-hni 039×2、advisor-pure-chat、omp-scope-seam 033 Keep、capability-enforcement 006）由 `remainingScopeTurnsEstimate` 改为 `newScopeRetentionBudget`（资源 `host/scope-steer/missing-estimate` 现行文案）；advisor-pure-chat 两项负向都保留。
  - 独立 resetScope 工具用例：omp-scope-seam 049、043 → skip；L187/L437 原已 skip；delegation-034 里死变量 `resetDef` 删；OpenCode scope-hni 047／050／043×4、context-compression/043 两个宿主接线例、advisor/scope-recovery/007 → skip [PRODUCT-RETIRED standalone-resetScope]；delegation-negotiation-004 去掉过早独立 reset 一步（注解路径等价未断言）。
  - proposals：Scope进度 §5／§9 补现行注，§6 注更正为「steer 资源沿用旧路径名、内容 retention」；跨Scope §1／§5／§6 补现行注。
  - scope-progress：不拆包；宿主三处 import（OMP Adapter、PluginHooks、ScopeReset）及 PluginHooks epoch 回退处加边界注释（仅 epoch 记账存活，estimate API 历史）。
- **仍欠**：scope-progress 包内 durable／estimate 实现与测（PACKAGE-HISTORICAL，拆包需另裁）；被 skip 的 OpenCode／OMP reset 档案链路（043／047／049／050）尚无注解路径等价新测；`arg-estimate`／`missing-estimate` 资源路径名残留；`requirements/**/WHAT` 中旧句未全扫；wx `ce-replay-wt`（勿贸然删）；上列裁定仍悬。
- **测况**（本机 build gen42 实跑）：pair-hint 018 5/5；repo 049 3/3、064 11/11；capability-enforcement 006 14 过 2 skip；context-compression/043 6 过 2 skip；omp-negotiate-physical 2 过 1 skip；advisor 007 系全过/skip。scope-hni-physical 17 过 8 败 8 skip（基线 15a7b718：14 过 19 败）；omp-scope-seam 11 过 6 败 7 skip（基线 10 过 9 败）。剩余失败均为基线既有：negotiate 回执文案（`Questions accepted`／`Clarification requested`）、OMP scope 协议标题（`Scope & Context Management`）、negotiation-005 期望 `resetScope` 在 agent config 显式 deny、delegation-034 期望「resetScope unavailable until first negotiate」——需另判是测旧还是产品回归，未臆改。
