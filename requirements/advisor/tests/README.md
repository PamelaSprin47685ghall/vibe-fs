# Advisor 测试索引

行为合同位于 [../WHAT.md](../WHAT.md)。本目录测试用 `node --test` 运行；它们验的是下述行为，不是「证明」。

`004.test.mjs`：经公开 `wanxiangli/scope-progress` 接口验证四段问答、同一顾问席位延续对话、自然提示内容、建议 steer、重复报告去重和过期 Scope 拒绝（组合协议的调用次序，不含真实商业 Provider 或跨进程恢复）。

`006.test.mjs`：经真实 JS 生成与执行产物验证旧 `this.advisor` 不在模型可见和运行成员中，伪造 `Advisor` 能力不会启用它，遗留程序调用不能抵达专席。

`007.test.mjs`：通过真实 OpenCode 插件确认 SQLite 受理与阶段预留先于 SDK session.create／prompt；SDK Provider 错误若没有确定终局回执，只留下 Admitted／StageBegan，不写 Finished(Failed) 也不重复付费请求；同报告重放不追加新受理。还验证三个不同 Pending 报告只需一次恢复就依次完成、原合并 Process 的精确持久 reportIds 冷恢复、首项 Unknown 禁止抢跑、SDK CapacityQueueFull 一类未确证结果不得错误提交 Failed；真实 OpenCode 物理 SDK 失败也只保存 StageBegan，不发第二次付费咨询。

`008.test.mjs`：用真实 PromptDispatcher/AgentJournal 的 Await 发送与物理接纳观测，验证 SDK 提交回执不能冒充物理落地、物理结果 Unknown 保留持久 Pending；同时验证 Advisor steer 用 Await 而普通协商保持 Detached。

`009.test.mjs`：直接调用生产 `HostForkBusyNudge.sendObservedAwait`，用真实 Host Session port 和 AgentJournal 验证只有 `PhysicalAccepted` 才返回 F# Ok，送达未知保留 PromptKey；另验证接纳观察预算被采用、超预算的迟到落地报 Unknown 而非 Completed。

`010.test.mjs`：直接经生产 `ToolRuntimeScope.SendAdvisorSteer` 验证排队回执不完成、物理落地才成功、等待期间 Scope 推进拒绝旧建议成功、Host Unknown 保留原始未决 claim。

`011.test.mjs`：用真实 AgentJournal／ToolRuntimeScope／SQLite 完成 A／B／C／D＋物理 steer 纵切：先五个 StageBegan/StageAnswered，最终落地后才有 Finished。

`012.test.mjs`：验证真实 OpenCode chat.params 只有已确证的当前 Scope epoch 才能认领旧 SQLite Pending，已发但回复未知的阶段不再调用 Provider；同一报告不能分配给两个不同 Process。

`013.test.mjs`：经注册的 OpenCode resetScope 真正提交 ScopeReanchored 后清空全部内存计费状态，验证下一宿主请求直接用 AgentJournal 身份认领旧 Pending，未确认外部阶段不重发、未知余额不伪造。

`014.test.mjs`：验证真实 OpenCode todowrite → Recorded/Settled → 清内存 → 恢复旧估计与 ResetDue 的纵切；畸形程序 admission 不计费、不启动 Advisor。

`015.test.mjs`：在两个同时存活的真实 OpenCode 插件工作区之间交替执行 todowrite，验证按 Session 绑定的 SQLite 不会被第二插件初始化覆盖。

`../host-boundary/tests/scope-hni-physical.test.mjs`：`delegation-negotiation-005` 验证专席零工具的真实角色配置与产品准入；`035`—`037` 验证临时问询保持原工具 schema、租约隔离以及失败不转成聊天内容；`advisor-031` 验证顾问不能再次派生顾问。

`wanxiangli/src/scope-progress/tests/001–003.test.mjs` 验证计费回合聚合、队列合并、四段顺序、迟到与失败释放。`004.test.mjs` 经真实 SQLite 验证受理和终结的 Committed／Replayed／Conflict／Unknown、SIGKILL 重读、跨两个独立 AdvisorFlow 队列与独立 OS 进程的全局单飞、确定性修订号竞争重试、终局抛错保护、跨强杀只读 Pending 和完成后恢复准入。`005–006.test.mjs` 验真实 SQLite 冷重开时复用已确认 prose、未知请求禁止重发及物理 steer 独立预留。`007–008.test.mjs` 以真实 SQLite 的 CAS、跨实例与 SIGKILL 验证保存普通 Provider 计费回合的最小估计、重启后上一回合的比较基线、ResetDue 与已结算但尚未派发的 AdvisorDue outbox。

**未覆盖**：实际供应商 ProviderRunId 与 SDK 物理请求结果的强杀后重新认领（当前只有阶段逻辑请求预留及已确认 prose）；没有新工作边界时的独立恢复调度；完整合并等待项冷恢复；商业 Provider 端到端。OMP 侧的对应测试在 `wanxiangli/src/scope-progress/tests/`（Adapter 模拟回合、shutdown/restart 自动结算、Bun/OMP SDK + 本地 SSE Provider 的四段网络纵切）；本地 SSE 假 Provider 回合不代表线上商业 Provider 回执，单次持久受理与全局单飞也不等于整个四段流程 exactly-once。
