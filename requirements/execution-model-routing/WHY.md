# execution-model-routing — WHY

总流程里，agent 每回答一次用户都要占一个真实的模型并发位置。本包回答的是：位置给谁、何时给、何时还，出错时谁来结算。

Runtime 只维护事实：IdentitySeed 确立的固定 canonical role 与 participant、当前占用的 target multiset `running`、被原子取代的活跃执行 target `previous`（其余为 null）。模型选择权在宿主：生产环境直接采用 OpenCode 官方机制解析出的模型（用户选择优先，其次 `opencode.json` / `opencode.jsonc`）；`wanxiangli/routing` 的调度器 ABI 只留给测试注入，选择策略不写死在产品代码里。

要点与理由：

- 无 fallback 池、无拉黑：错误与催促重试沿用同一模型，避免模型漂移与配置多源分叉。
- 租约绑定 `(SessionId, PhysicalUserMessageId)` 而不是 session：idle 会话不会永久霸占物理容量，同一物理消息的重试不换 target、不换角色。
- 容量 token 与执行绑定解耦：token 只凭显式 lender 借用，只在 provider-step 边界转移；`null` 是并发占满时的等待（背压），不是执行失败。
- 先 durable 接受、再占容量：未接受的发送意图不预占资源。
- 终结保守：只有 Host 明确给出的成功终结理由才解除 physical binding；含混或失败归一化只结束单步，让 Host 能用同一材料继续重试。
- 严格区分本地 participant（Role/Persona/SelectedAgent）与远端 ModelTarget：执行身份与容量归属才不会撕裂。

破裂后果：租约与 session 错误绑定导致容量被 idle 会话霸占；重试期间模型漂移；凭缺席或错误的 lender 借用导致容量归属错位；`null` 被误判为 provider 失败而烧掉重试预算；跨进程失去真实并发视图，向底层 provider 超额发牌。

相邻：`participant-identity`（canonical participant 定义）、`managed-session-lifecycle`（会话销毁信号）、`managed-chat-execution`（durable 接受）、`execution-failure-policy`（失败结算）、`host-boundary`（plugin 启动、hook 拦截与消息改写）。

Routing 只拥有 exact capacity fence 与 execution binding 的结算；进程级终止不属于路由器，fatal 只能经组合层注入的 capability 触发一次报告与终止（WHAT 的 016 条已标注该接线尚未落地）。
