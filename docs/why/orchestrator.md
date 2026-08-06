# Orchestrator — 理由

Clean Gate 拒绝「脏工作区上猜用户意图」，把编排建立在可命名的 Git 事实上。

Integration Gate 缩到 ref mutation，是为了不在长 Review 期间串行化所有 Job：并行 rebase/review 合法，只有推 ref 需要 CAS。

恢复按 Journal 最后事实单出口，避免「读一下磁盘猜进度」——磁盘状态可伪造，事实链不可跳步。PublishClaimed 三分支顺序固定，是因为 CAS 窗口崩溃后「已 ff / 未 ff / 过期」必须穷尽且互斥。
