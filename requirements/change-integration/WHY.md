# change-integration — 为什么必须独立存在

## 不可替代的存在理由

系统让多个独立 Manager job 各自在 worktree 里成熟，最终都要把 candidate 合入**同一个共享 target
ref**（如 `master`）。这个「独立发展 → 共享入口」的收敛点必须由本包保证。没有本包：

1. **并发 publish 互相覆盖**：两个 job 同时推 ref，后写覆盖先写，或先写覆盖后写——共享世界进入
   无人声称的状态。
2. **安全靠全全局锁**：为了不覆盖，把长 review / 冲突修复全部塞进全局锁——所有并行 job 被串行化，
   独立工作失去并行意义。
3. **脏工作区上猜意图**：在未命名的 Git 状态上做编排，恢复时无法复现用户意图。
4. **崩溃后不可判定**：CAS 窗口崩溃后分不清「已 ff / 未 ff / 过期」，恢复只能猜。
5. **磁盘状态冒充进度**：恢复时扫文件系统反推「进行到哪一步」，磁盘可伪造、可停写。

RED 判定：并发 publish 可互相覆盖，或系统为了安全把长 review/repair 全部塞进全局锁。此时世界 RED。

## 独立变化测试

从 worktree+rebase 改为另一 candidate integration strategy（例如 merge queue / stacked PR），而
publish/CAS semantics 不变——本包 WHAT 全部不动。反之，改 gate 的物理锁实现（proper-lockfile →
其它），语义合同不变则 WHAT 不动。

## 历史失败模式

- **全 Job 串行 vs 只锁 ref mutation**（历史 why/orchestrator 备选节）：曾考虑把 Integration Gate
  提前到 `runManagerJob` 入口「先锁后干活」，会在远端长 Review 期间阻塞所有 Job 的并行 rebase。
  拒因：Gate 缩为短 CAS 只保护 ref 变更（ORCH-005）。
- **脏工作区上猜意图**（ORCH-002 备选）：自动 stash / 自动 commit / 猜用户意图清理——拒因：编排必须
  建立在可命名的 Git 事实上，否则恢复无法复现意图。
- **读磁盘猜进度**（ORCH-007 备选）：用文件系统状态反推进度——拒因：磁盘状态可伪造、可停写；
  事实链不可跳步（PERSIST-009 / `durable-events`）。
- **PublishClaimed 三分支折叠**（ORCH-007 备选）：CAS 窗口崩溃后无法区分「已 ff / 未 ff / 过期」，
  折叠会造出不可判定的中间态。拒因：三分支穷尽且互斥、顺序固定不可换。
- **e2e 超时先放大再查因**（历史 change（orchestrator-e2e-timeout））：三 canary watchdog 超时
  （`orch.2`/`manager.3`/`manager.4` blocked expectations）——根因是 companion blogger flights
  per-plugin-instance 与 blogger sessions 在 RootWorkspace 下脱节，Finality 挂在 `journal-work-log`；
  修复 = 因果 frontier（`SharedState.BloggerFlights`），**不是**把 `check:release` 缩水成 targeted
  canary 冲绿。教训：恢复与发布路径的可解释性优先于绿。

## 与相邻包的边界

- Git road 发布的**效果记账**（Requested → Accepted/Published、unknown outcome 分型）→
  `effect-accounting`（COVERAGE 裁决：Git road 发布走 effect-accounting，不归本包）。
- 恢复所需的 durable facts 与确定性 fold → `durable-events`；崩溃后重入普通程序 →
  `crash-reconciliation`。
- 道路语义（谁拥有 road、何时续做/新开）→ `delegation`；本包只拥有「道路如何进入共享 ref」。
- review judgement 本身（PERFECT/REVISE 判断标准）→ `review-judgement`；witness 有效性 →
  `review-assurance`。
