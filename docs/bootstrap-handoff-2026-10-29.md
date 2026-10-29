# 指南自举交接（2026-10-29 JST）

| 仓 | HEAD | 备注 |
|---|---|---|
| vibe-fs | `5666ea1e` | 删 resetScope 工具；清 estimate；beat 别名 |
| wanxiangli | `31cc992` | beat aliases + seed; ResetScope 无独立工具名 |

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
**已做：** OMP 全工具 scope 注入+校验；removeLast 出站坍缩（OpenCode+OMP）；Advisor 改挂 retention advisorDue（streak≥3 / remaining=0）；冷启 journal 绑定+ensureEpoch 重放；estimate 改为可选；Stall/ResetDue 不再开顾问；steer 文案改 retention；build gen39。

**过渡已清：** 独立 `resetScope` 工具已物理删除；档案重锚走任意工具 `scope.kind=resetScope` → `ScopeReset.commit`；`remainingScopeTurnsEstimate` schema/驱动力已删；removeLast 投影用 beat 别名（providerRunId/callId）兜底。PairHint 过渡期仍只认旧工具名屏障。wx `ce-replay-wt` 仍只记债。
