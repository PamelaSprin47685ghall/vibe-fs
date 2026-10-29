# 指南自举交接（2026-10-29 JST）

主仓 HEAD：见本轮 push 后 SHA。本机曾对齐 origin `5dd99b875`；Scope 落地后另有新提交。
wanxiangli：`e5e68a1`（含 `scope-retention` + pair-hint retention 文案）。
本机 stash 已命名保留：
- 主仓 `stash@{0}`：`handoff-2026-10-29: 主仓 PluginTransforms/managed-chat/ProviderLifecycle 半成品（已被 origin 5dd99b87 覆盖，含旧版 018）`
- li `stash@{0}`：`handoff-2026-10-29: li failure 半成品（已被 origin c8d18b9 覆盖）`
wx 现场干净；tmux / opencode 已停；指南自举巡视 routine 已删。

## DevOps 已收口

E1/E2 去证明税两刀已推：子模块曾 `c8d18b9`，主仓曾 `5dd99b875`。畸形 carrier→缺席；authority 缺失→响亮失败。

## 仍悬着的自举义务

1. **两项设计裁定追认**（E1 已按 A+A 落地；若否决需回滚）
2. **phase/step 逐处裁定**（约 22 处；档案 Engineer 未收回，需重派）
3. **委托惯例「验证前查总工头合入」** 未落持久文档
4. **「22 处」清单完备性**核对
5. **wx 残留工作树** `wanxiangli-ce-replay-wt` 占 main@f1d01a0
6. **上游额度**曾导致 Manager 空转

## Scope annotation（本轮已落地部分）

- 共同法 «Scope»、scope-protocol、续工/重启/AGENTS 已互锁改写
- `wanxiangli/scope-retention`：reset/keep/remove/meToo、硬顶 48、BeatRetracted journal、空转 streak≥3 advisorDue；测 001 6/6
- OpenCode：`scope` 钩子注入全工具 + ScopeAnnotationGate 校验；todowrite allowlist 放行 `scope`
- PairHint：retention 文案 + setRetention 同步显示额度
- **未完**：OMP Adapter 同构注入；removeLast 出站整拍投影接到 PairHint planFilter；删旧 resetScope 工具 / remainingScopeTurnsEstimate / Stall·ResetDue；Advisor 改挂 streak≥3；宿主冷启 journal 重放接到真实 eventstore
