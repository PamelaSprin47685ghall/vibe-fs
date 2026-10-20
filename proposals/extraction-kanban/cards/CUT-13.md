# CUT-13　委派、复用与结果认领

返回 [BOARD](../BOARD.md) · [CARDS §CUT-13](../CARDS.md#cut-13)

## 1. 当前状态与前置核对

- **当前列状态**：`DOING`（eng-cut12c-goose，2026-10-21）。子闭环 1（handle 与 work 格的单赢家生命周期律）已落地；其余见 §5。
- **前置**：CUT-06 DONE；CUT-12 DONE（同一棒落地）。
- **边界**：handle/work 表、键查找、blob ref/digest 与完成格的值相等、typed 记录构造、事实写入都留在主仓；库只拿当前相位、遗留标记、完成载荷的形状（kind、ref/digest 是否在）与相等事实，给出 set/keep/reject。不新开第二份账：`AgentLinkageProjection` 仍是唯一事实账。

## 2. 库侧

- wanxiangli `61968a4`：新模块 `src/handle-lifecycle`（`HandleLifecycleLaw.handle` / `work`，npm 入口 `wanxiangli/handle-lifecycle`），AGENTS.md 写 AXIOM/LEMMA/SPEC，`index.d.ts` 写 plain 契约；`ea20725` 发布 **0.52.0**。
- 库测试 `tests/001.test.mjs` 6/6（含卡片反例：第二个终局覆盖、join 退役 Active、旧 handle 结算新 work、空白 consumptionId、坏补偿撤销真完成）；build-001 独立编译 1/1。

## 3. 主仓接线

- `Execution/Delegation/LinkageProjection.fs`：`HandleProjection.complete/abandon/retire/rejectFalseCompletion` 与 `completeWork/abandonWork/voidWork/consumeWork` 的相位判断交库；本地只查格、算 `cellMatches`/`cellEqualsExpected`、按裁决写自己携带的完成、原因、consumption id。删 `verifyFalseCompletionCell`、`updateWork` 与八处手写 match。
- 未动：`linkNamed`（绑定身份一致性）、`admitWork`（准入需 PromptAuthority 落地核对，属 CUT-09/11 事实），列表/可见性投影。

## 4. 验收（§9）

- base = master `b10da13c9`；candidate 同 CUT-12 §9（gitlink 0.52.0），`node scripts/build.mjs` exit 0。
- 差分 [handle-differential.mjs](../evidence/CUT-13/handle-differential.mjs)：handle 格 absent/Active/每种完成/每种放弃原因/Retired × 四个 handle 事件（每种完成、原因、期望 ref×digest），work 格同样的相位 × 遗留与否 × 四个 work 事件（每种完成、三种 consumption id × 每种期望完成），比较整个 Result（拒绝或完整投影）：**8,547 次 0 处不同**。
- 变异 [mutants.py](../evidence/CUT-13/mutants.py)：对 candidate 实际加载的库产物做 9 个语义变异，全部报红（[mutants.out](../evidence/CUT-13/mutants.out)）。
- 套件：delegation 全部与 CUT-12 同批复核，见 [evidence/CUT-12](../evidence/CUT-12/README.md)。
- 净改进：EXEC-004/EXEC-009 的单赢家规则原先在 handle 与 work 两层各写一遍（八个 match，拒绝次序靠手抄一致）；现在只有库一个 owner，带编号测试、差分与变异证据；行为与持久字节不变。
- 持久数据（§10）：事实 schema 与折叠结果不变；无迁移。回退：revert 主仓提交。

## 5. 余项（本卡仍 DOING）

1. 固定 DevOps resume（`SyncDelegate/Store`、`Runtime` 的复用判定）。
2. SyncDelegate 同步委派的等待与结果认领（`SyncDelegate/Workflow`）。
3. restart/旧回执与回收（`Fork/ChildRecovery`、`ChildRecoveryWorkflow`、`Fork/Host/Restart`）。
4. Orchestrator commission 的 Git 发布职责交 CUT-23，不入委派库。
