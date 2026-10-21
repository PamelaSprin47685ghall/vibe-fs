# CUT-09-b　interaction-authority-018 关闭见证的精确匹配

返回 [BOARD](../BOARD.md) · 母卡 [CUT-09 §6](CUT-09.md#6-收窄范围与移交done-的边界)

## 1. 当前状态与前置核对

- **当前列状态**：`DONE`（范围按 §5 收窄；eng-cut25h-goose，2026-10-21，协调者改派自 eng-cut12d 一路）。
- **前置**：CUT-09、CUT-11 均已 DONE。
- **出发点（master `ca9e14c29` 实测）**：宿主唯一的关闭 writer（`JournalSurface.appendAuthorityLogicalRunClosed`）手写过滤
  `CanonicalRole = Manager && AuthorityKind = HumanRoot`，把**裸 retirement id** 当作 `ClosureWitness` 写入；durable 读回
  （`Fold.foldAuthorityLogicalRunClosed` → `PromptAuthorityRun.replayLogicalRunClosed`）**完全不看 witness 与 RootKind**，
  只按 (LogicalRunId, AuthorityRootId) 关闭。五类 `ExpectedClosureKind` 与各类 source witness 在全仓没有任何表示。

## 2. 库侧（wanxiangli `079e136`，发布 **0.57.0** / `485d854`，已在 origin/main）

`src/authority/ClosureLaw.fs` 成为下列规则的唯一 owner，`Authority` 增加 plain JS 边界，`index.d.ts`、`AGENTS.md`（LEMMA 6、SPEC 6–8）同步：

| 规则 | 入口 |
|---|---|
| 五类闭集合；HumanRoot × Manager → HumanRootManagerLife，其余 HumanRoot → HumanRootManagedRun；AgentOwnerRoot 按 child / attached / internal-leaf 归后三类（InternalLeaf 不论物理 Ownership）；无法唯一归类 → null（不得被接受） | `expectedClosureKind` |
| 五种 source witness 的唯一拼法（JSON 字符串数组，标签在前，按类定长）及其逆 | `encodeClosureWitness` / `decodeClosureWitness` |
| 精确匹配：RootKind 与类别一致；witness 属本类；owner 运行 / 被关运行 / Manager life 逐字相同；字段非空；根缺 owner 或 life 时拒绝而非猜测 | `admitClosure` |
| 旧格式：CUT-09-b 之前唯一 writer 写的裸 retirement id，只在 HumanRootManagerLife 下读作 `LifeCompleted(life, id)`（legacy），别类一律 malformed | `admitClosure` |

库测试 `src/authority/tests/003.test.mjs`（oracle 按 WHAT [018] 独立写）5/5；去掉 owner 比较的变异使其变红、还原复绿；发布脚本里 authority 全部 + exports 17/17，打包后消费方 import 冒烟通过。

## 3. 主仓接线（宿主先补事实，再交库）

- **新事实**：`AuthorityLogicalRunClosed.ClosureWitness` 从裸 retirement id 改为规范的 `["LifeCompleted", <Manager 的 session>, <交付的 S4 retirement id>]`
  （`PromptAuthorityRun.managerLifeClosureWitness`，拼法归库）。写入点不变（`appendAuthorityLogicalRunClosed`，单 writer、缺共享条目拒绝、已关闭 no-op 都不变）。
- **写侧过滤**交库：手写的 `Manager && HumanRoot` 判断删除，改为"活跃根的预期类别正是 HumanRootManagerLife"。
- **读侧精确匹配**：`replayLogicalRunClosed` 增加 `rootKind`、`closureWitness` 两个参数，在会关闭活跃运行的那一支先过库的 `admitClosure`；
  别类、别的 life、畸形 witness、RootKind 不符都 fail-closed（fold 拒绝该事实，活跃运行保留）。已关闭 / 无权威两支行为不变（幂等重放）。
- gitlink `wanxiangli` `016e5f1`（0.48.0）→ `485d854`（0.57.0）；lockfile 只改 wanxiangli 版本行。

## 4. 反例与验收（§9）

- `requirements/interaction-authority/tests/018.test.mjs` 新增 5 项真测试（durable 链：真实共享 journal、S4 交付、落盘后改写 witness、冷重开）：
  canonical witness；别类 witness / 别的 Manager life / 畸形 witness 不得释放活跃运行；旧格式裸 id 仍关闭它所属的 Manager life。
  - 候选：10 pass / 0 fail / 2 todo（`evidence/CUT-09-b/018-on-candidate.txt`）。
  - 同一测试文件放到不含本刀的 master dist 上：5 项新增全红（`018-on-master.txt`）——其中「别类 / 别 life / 畸形 witness 不释放运行」三项是真实缺陷：
    旧 fold 不看 witness，任意 witness 都会关闭运行，`probe-tampered-witness.mjs` 也复现了这一点；另两项红是因为旧 writer 写的是裸 id（格式差异）。
- 定向套件 interaction-authority、participant-identity、dispatch-protocol、managed-session-lifecycle、work 全部：649 项，546 pass，34 todo；
  逐文件隔离重跑后候选 68 项 not ok 与基线（不含本刀的 dist）68 项**名字完全相同**（`targeted-failures-{candidate,baseline}.txt`，diff 为空），
  集中在 managed-session-lifecycle 006/007/008/013/014/015/018/022/028 与 dispatch-protocol 003，是 master 既有问题，本刀未触及。
- `scripts/checks/architecture.mjs` OK。

## 5. 收窄范围与移交（DONE 的边界）

| 余项 | 为什么不在本卡 | 去向 |
|---|---|---|
| HumanRootManagedRun、AgentOwnerChildWork、AgentOwnerAttachedWork、AgentOwnerInternalLeaf 四类的 durable source terminal 与对应 closure writer | 宿主今天**没有**这四类的 durable closed outcome，也没有任何 writer；这是 GAP-123 的产品缺口（新功能），不是可抽取的既有职责。库已备好这四类的类别、witness 与匹配，宿主补出 terminal 后直接调用 | GAP-123（OPEN），018 两个 todo 锚点保留 |
| `ExpectedClosureKind` 在 `AuthorityRootAccepted` 中原子记录 | AgentOwnerRoot 的生命周期归属（child / attached / internal-leaf）在接受点宿主尚无记录；现在由库按活跃档案推出（HumanRoot 两类可推，AgentOwnerRoot 无类别 → 不可关闭，失败关闭）。改 durable schema 属 §10，随上一行一起做 | GAP-123 |
| `DelegationProjectionBridge` 在投影里按子工作 terminal 关闭 AgentOwnerRoot（`closeCompletedAgentOwnerChildWork`） | 它不写 durable closure，属 CUT-13 的委派 / 结果认领链 | CUT-13 |
