# execution-model-routing — WHAT

## EMR-001: 唯一模型调度 authority = `~/.config/opencode/wanxiangshu.mjs`；缺失时原子创建推荐模板

Wanxiangshu 的 managed 模型调度由 `~/.config/opencode/wanxiangshu.mjs` 的 default export 唯一决定。禁止从 `opencode.json`、环境变量、Host-final agent inventory 或内建表中覆盖或回退。
若文件不存在，系统在加载时确保目录存在并原子创建推荐策略模板，随后加载该文件。已有文件严禁自动覆盖。文件缺失、加载失败或导出非法时直接 fail closed。

## EMR-002: scheduler ABI 只有 `role + running + previous → target | null`

调度函数的唯一签名合同为：
```js
export default function route(role, running, previous) { ... }
```
- `role`：当前请求的 managed EffectiveAgent 精确名称。
- `running`：当前进程所有活跃 provider capacity token 的 ModelTarget multiset，元素形状固定为 `{ model: string, reasoning: string }`，保留重复项。
- `previous`：同一未删除 Session 最近一次成功物理执行所使用的 target（新 Session 传 `null`），作为接续偏好提示，不占容量。
- 返回值：必须包含非空 `provider/model` 与 `reasoning`，或返回 `null`。
- 抛出异常、返回 Promise 或非法结构均视为配置错误，直接 fail closed。

## EMR-003: `running` 是真实 provider capacity token multiset；不是 live-session / active-execution 计数

`running` 准确反映系统内部实际持有的 provider capacity token 集合。基础 token 总数即为 `running.length`。同一进程内所有 plugin 实例与 worktree 共享该 module-level multiset 真相。borrower 与 lender 共享同个 token，不得重复计数。

## EMR-004: required execution demand 只在 `chat.message` 物理执行准入产生；`null` = 等待，不是失败

发送或排队阶段严禁抢占 model slot，`SendPrompt` 必须保持 `Model=None`。唯一合法的需求准入点是 Host 接收物理 user message 后的 `chat.message` 边界。
若调度器返回 `null`，不调用 provider、不消耗失败预算、不盲目降级，demand 进入 pending 队列并在 occupancy 变更时由事件驱动重算。新到达的物理 user message 或会话销毁将取消并取代被 supersede 的旧 pending demand。

## EMR-005: 模型选择策略全部属于 MJS；runtime 不再拥有 lane、容量表或候选算法

Runtime 仅负责加载 scheduler、校验 ABI、维护进程共享的 token ledger 与借贷仲裁，不拥有任何模型分类、优先级表、容量上限或调度策略。一切关于模型选取与并发限制的逻辑均属于 MJS 策略。

## EMR-006: managed lease 只在一个物理 execution 内稳定；session continuation 重新调度但可偏好上一 target

物理执行租约与 `(SessionId, PhysicalUserMessageId)` 绑定。同 physical id 的重试沿用原 target；同一 SessionId 出现新 physical id 时原子替代旧租约，并将上一 target 作为 `previous` 传入调度器供其优先续用。租约不以 SessionId 为单位跨物理执行永久绑定。

## EMR-007: physical execution identity / end evidence 释放 occupancy；session/业务 lifecycle 不拥有槽

租约释放必须依赖确切的物理执行终结证据（无 error 且非 `tool-calls` 的 completed assistant message，且其 parentID 匹配 PhysicalUserMessageId）。
`finish="tool-calls"` 仅终结单步并归还 step token，不解除 physical execution binding；assistant error 仅作为单步失败归还 step token，不直接删除 execution binding 以便 Host 进行同 material 重试。业务层的 handle 完成、join 或 finality 不直接操作租约。

## EMR-008: `opencode.json` model 不再具有 authority；不校验 fast/deep model 互异

Host 的 `opencode.json` 不作为 managed model 的真相源。系统不要求 fast 档与 deep 档使用互异的物理模型字符串；两者解析至相同 target 属于合法状态。

## EMR-009: `chat.message` 是唯一 managed model admission；dispatch message 保持 model-free

所有内部 synthetic prompt 分派均保持 `Model=None`。Host 接收物理 user message 后的 `chat.message` hook 负责获取租约，并将 `{providerID, modelID, variant}` 投影至 mutable message。后续 `chat.params` 仅验证当前物理执行已记录的确切绑定。

## EMR-010: provider capacity 独立成可抢占 token；只沿 session lineage 借用

ModelTarget 物理绑定与 provider capacity token 严格解耦。在 provider 请求发出前，`experimental.chat.messages.transform` 负责获取对应 provider 的 capacity token。
子 session 可借用祖先在等待时的闲置 token；token 仅在 provider-step 边界转移，祖先召回时需等待子 step 结束。Blogger 伴侣执行使用与 Main 同源的平行 companion credit 借用机制。
