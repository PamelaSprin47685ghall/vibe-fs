# CUT-13　委派、复用与结果认领

返回 [BOARD](../BOARD.md) · [CARDS §CUT-13](../CARDS.md#cut-13)

## 1. 当前状态与前置核对

- **当前列状态**：`DONE`（eng-cut12d-goose 接手 eng-cut12c-goose，2026-10-21）。三个子闭环都已落地，见 §5 与 §6；余项去向见 §7。
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

## 5. 子闭环 2：restart 后委派子会话的恢复决议

- 库：wanxiangli `4559413` 给 `handle-lifecycle` 加 `recoverChild()`（AGENTS.md LEMMA 增补），`31460ca` 发布 0.53.0；主仓接线落在 0.54.0（gitlink `342de4d`）。
- 主仓：`Execution/Delegation/Fork/ChildRecovery.fs` 的 `resolveChild` 只把持久证据种类、快照种类、终局证明是否成立和 Host 观察种类交给库；决议次序（持久证据先于快照、快照先于观察；放弃原因优先于 SessionActive；RecoveryInFlight/仅 abort/未知都只是 incomplete）只剩库一份。终局证明仍由主仓 `JoinableCompletion.tryFromProvenTerminal` 校验，库只拿“成立/不成立及原因”；删 `resolveTerminal`/`resolveNonTerminal`/`resolveSnapshot`。
- 差分 [recovery-differential.mjs](../evidence/CUT-13/recovery-differential.mjs)：7 种持久证据 × 7 种快照 × 长度 ≤ 3 的全部有序观察列表，比较整个 `ChildResolution`：**12,691 次 0 处不同**（[out](../evidence/CUT-13/recovery-differential.out)）。
- 变异 [mutants-recovery.py](../evidence/CUT-13/mutants-recovery.py)：6/6 报红（[out](../evidence/CUT-13/mutants-recovery.out)）；其中 bad-proof-trusted 是 candidate 直接抛错（主仓拒绝库给出它没有的终局），算响亮失败，不算差分数字。

## 6. 子闭环 3：SyncDelegate 语义批次准入（同步委派的等待与认领入口）

- 库：wanxiangli `2aee0fb` 新模块 `src/delegate-batch`（`DelegateBatchLaw.admit` / `observedBatch`，npm 入口 `wanxiangli/delegate-batch`），AGENTS.md AXIOM/LEMMA/SPEC，`index.d.ts` plain 契约，库测试 5/5、build-001 独立编译 1/1、exports 3/3；`ea877d9` 发布 **0.55.0**。
- 主仓：`SyncDelegate/Store.fs` 的 `admitSingle`/`admitBatched`/`resolvePendingBatch`/`completeOrWaitBatch`/`sameCallOrder`/`batchOfObservedRun` 判定删去，`SyncDelegateCallStore.Admit` 与 `TryObservedBatch` 交库判：已有批次拒、未分组遇 pending 拒、非法批次拒、别的 ProviderRun 拒、重复 ToolCallId 拒、收齐才按 provider 次序整批准入。pending/active 缓冲、invocation 与 TaskCompletionSource 仍是主仓的物理资源；拒绝文本逐字不变。
- 差分 [batch-differential.mjs](../evidence/CUT-13/batch-differential.mjs)：base 与 candidate 两个真实 `SyncDelegateCallStore` 跑同一组 20,000 条随机操作序列（Admit/ReleaseAdmission/ObserveProviderToolCall/TryObservedBatch，2 scope × 2 role × 两个 ProviderRun，含空、重复、外来 current 的批次），每一步比较答案：**240,000 次 0 处不同**，每种拒绝、Waiting、Ready×1/×2/×3 与观察批次都有覆盖（[out](../evidence/CUT-13/batch-differential.out)）。
- 变异 [mutants-batch.py](../evidence/CUT-13/mutants-batch.py)：10/10 报红（[out](../evidence/CUT-13/mutants-batch.out)）；order-ignored、ready-early、never-opens 三个是 candidate 抛错（主仓拿不到库声称已收齐/已开的缓冲），同样算响亮失败。

## 7. 验收（§9）与余项去向

- base = master `ac046ae88` 的构建（`CUT-12d-base`）；candidate = `72f51f635`（gitlink 0.55.0），`node scripts/build.mjs` exit 0，`scripts/checks/architecture.mjs` OK。定向套件（与 CUT-12 同一份 126 个测试文件）candidate 失败名 125 个，全部也在 base 失败名（129 个）里，没有新增失败。
- 净改进：委派的三条规则（handle/work 单赢家、restart 恢复决议、同步委派批次准入）原先散在 `HandleProjection`、`ChildRecovery`、`SyncDelegateStoreOps` 的手写 match 里；现在各只有 wanxiangli 一个 owner，带编号测试、差分与变异证据；行为、拒绝文本与持久字节不变，无迁移。回退：revert 主仓提交。
- 库草稿 `src/delegation` 已退役（wanxiangli `d4ff4fb`、`e8fa66a`）：完成单实例认领由 handle-lifecycle 证明，批次准入由 delegate-batch 证明。
- 余项去向（不是遗漏）：
  1. 固定 DevOps resume 的复用判定走 `IAttachedSessionPort.GetOrCreate`，已由 CUT-12 子闭环 ③ 交 `wanxiangli/attachment-lease`（exact 租约复用与新子回收），本卡不再复制一份。
  2. 批次答复的扇出（canonical 拿 view、兄弟拿 `MergedInto` 首个调用）与 terminal 只让 fresh root 失败当前 call，是对 TaskCompletionSource 与 Host terminal 订阅的物理结算，RETAINED 在 `SyncDelegate/Workflow.fs`；它们只读库已经定下的批次与 `TerminalStop.belongsTo` 事实。
  3. Orchestrator commission 的 Git 发布职责交 CUT-23，不入委派库。
