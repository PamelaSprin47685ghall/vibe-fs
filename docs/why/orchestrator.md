# Orchestrator — 理由

Clean Gate 拒绝「脏工作区上猜用户意图」，把编排建立在可命名的 Git 事实上。

Integration Gate 缩到 ref mutation，是为了不在长 Review 期间串行化所有 Job：并行 rebase/review 合法，只有推 ref 需要 CAS。

恢复按 Journal 最后事实单出口，避免「读一下磁盘猜进度」——磁盘状态可伪造，事实链不可跳步。PublishClaimed 三分支顺序固定，是因为 CAS 窗口崩溃后「已 ff / 未 ff / 过期」必须穷尽且互斥。

## 备选与被拒

**工作区：Clean Gate 拒绝脏 vs 脏上猜意图。** 拒脏猜：编排必须建立在可命名的 Git 事实上（ORCH-002）；否则恢复无法复现用户意图。

**Gate 粒度：全 Job 串行 vs 只锁 ref mutation。** 拒全串行：远端长 Review 期间会阻塞所有 Job 的并行 rebase。Integration Gate 缩为短 CAS 只保护 ref 变更（ORCH-005）。

**进度来源：Journal 最后事实单出口 vs 读磁盘猜。** 拒读盘：磁盘状态可伪造、可停写；事实链不可跳步（PERSIST-009）。

**PublishClaimed 三分支：穷尽互斥 vs 折叠。** 拒折叠：CAS 窗口崩溃后必须能区分「已 ff / 未 ff / 过期」，折叠会造出不可判定的中间态。
