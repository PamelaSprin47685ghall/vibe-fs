# execution-model-routing — WHY

## 领域动力与核心张力

模型调度的核心张力在于**物理资源占用的真实性**与**资源调度策略的多样性**之间的分离：

```text
Runtime 维护事实    ──► 当前机器角色 role、当前已占用租约 multiset running、上一成功 target previous
MJS 表达策略        ──► (role, running, previous) -> { model, reasoning } | null
```

如果将调度规则、候选列表、并发容量硬编码在 runtime 内，任何模型池调整或策略变动都会导致产品核心代码修改。反之，若由 runtime 猜测默认模型或允许多处配置覆盖，则会失去单一真相。

`execution-model-routing` 的核心不变量：
- **单一权威**：唯一配置源为 `~/.config/opencode/wanxiangshu.mjs`；缺失时自动原子生成推荐模板。
- **职责划分**：Runtime 仅维护真实 lease multiset 与串行仲裁；选择算法完全交由 MJS 纯函数表达。
- **租约生命周期绑定物理执行**：Managed lease 绑定至 `(SessionId, PhysicalUserMessageId)`，不以 session 为长期独占单位。
- **终结证据保守**：只有 Host 明确给出的成功终结理由才能结束 physical execution；`unknown`、`error` 等含混/失败归一化只结束 provider step，不能猜测 material 已永久结束。
- **背压语义**：`null` 表示当前并发占满的等待状态（backpressure），绝非执行失败或错误。
- **容量与绑定解耦**：物理执行绑定（ExecutionBinding）与请求容量令牌（Capacity Token）分离，容量仅沿 Session Lineage 受控借用并在 step 边界召回。

## 破裂后果

- 配置多源分叉，模型池变更破坏运行时核心代码。
- 租约与 session 错误绑定，导致 idle 会话永久霸占物理容量，或 retry 期间发生模型漂移。
- `null` 被误判为 provider 失败，错误消耗重试与降级预算。
- 跨进程或跨工作区无法共享真实的并发占用视图，导致向底层 provider 超额发牌。

## 边界与关系

- `participant-identity`：提供 CanonicalRole、EffectiveAgent 与 Peer 关系定义。
- `managed-session-lifecycle`：提供 managed session 生命周期边界与销毁信号。
- `host-boundary`：提供 plugin 启动、物理 message/hook 拦截与 Host 消息改写边界。

## DEPENDS ON

- `participant-identity`
- `managed-session-lifecycle`
- `host-boundary`
