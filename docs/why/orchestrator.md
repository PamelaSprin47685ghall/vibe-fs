# Orchestrator — 理由

Clean Gate 拒绝「脏工作区上猜用户意图」，把编排建立在可命名的 Git 事实上。

Integration Gate 缩到 ref mutation，是为了不在长 Review 期间串行化所有 Job：并行 rebase/review 合法，只有推 ref 需要 CAS。

恢复按 Journal 最后事实单出口，避免「读一下磁盘猜进度」——磁盘状态可伪造，事实链不可跳步。PublishClaimed 三分支顺序固定，是因为 CAS 窗口崩溃后「已 ff / 未 ff / 过期」必须穷尽且互斥。

Orchestrator 拥有道路，不拥有道路进入共享世界的机械。provider 面委托用 `commission(calling?, name, charge)`：独立集成之路；与 Manager 使命内 `fork` 硬语义不同，故不同名。成功只见 Byname 承接 charge——不见 `job_id`、worktree、`reused`、agent/role/tier/fallback_peer。朝向用同一 `horizon` contract。机器 Journal/CAS/worktree 精度全在墙内。

## 备选与被拒

**工作区：Clean Gate 拒绝脏 vs 脏上猜意图。** 拒脏猜：编排必须建立在可命名的 Git 事实上（ORCH-002）；否则恢复无法复现用户意图。

**Gate 粒度：全 Job 串行 vs 只锁 ref mutation。** 拒全串行：远端长 Review 期间会阻塞所有 Job 的并行 rebase。Integration Gate 缩为短 CAS 只保护 ref 变更（ORCH-005）。

**进度来源：Journal 最后事实单出口 vs 读磁盘猜。** 拒读盘：磁盘状态可伪造、可停写；事实链不可跳步（PERSIST-009）。

**PublishClaimed 三分支：穷尽互斥 vs 折叠。** 拒折叠：CAS 窗口崩溃后必须能区分「已 ff / 未 ff / 过期」，折叠会造出不可判定的中间态。

**委托工具：fork-manager DTO vs commission。** 拒旧 `agent=fast-manager|job_id`、worktree、`reused=true`：那是机器拓扑当世界语言，逼模型当 union decoder。选 Byname + calling + charge；reuse 靠 name/charge 识别，不暴露 id、不用 reuse flag。

**命名：commission 与 Manager fork 同名 vs 分名。** 拒同名：独立道路 ≠ 使命内证人；同名破坏「同名⇒同 contract」。

**Orchestrator 可见面：job_id/worktree 暴露 vs 永不进入 horizon。** 拒暴露：编排者若看见集成机械，会把 CAS/worktree 当 craft，污染「拥有道路、不拥有机械」的 epistemic 边界。

**朝向：list 状态机词汇 vs horizon。** 拒 status/id/kind/ordinal：朝向只需「谁还在远方 / 谁已归来」。

**身份：fast-/deep-orchestrator 进自称 vs 仅 Execution Binding。** 拒进 horizon：Persona 为 Integrator/Director；tier 标签是路由，不是自我。

**Provider 迁移：alias / 渐进双轨 / 机器 DTO 留 horizon vs clean break。** 拒前三者：双轨期间旧 fork-manager 面与新 commission 面并存，测试与 prompt 永远对齐泄漏面。一次断；旧符号删。Steward/Sphinx 不在本轮创建。
