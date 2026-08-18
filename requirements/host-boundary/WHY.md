# WHY — host-boundary

## 一句话

业务必须建立在外部 Host 可稳定证明的物理能力上，而不是流式噪声、私有实现、偶然 hook 参数。

## 不可替代的存在理由

1. **碎片事件拼真相 = 把因果绑在传输噪声上**（ARCH-002 被拒方案：流式碎片积分）。碎片事件的
   顺序/形状随 Host 版本漂移；从 `message.updated` / `part.delta` 推导完成/失败，上游一改事件
   shape，业务就悄悄读错。真相源必须固定为「唤醒后读完整 SDK snapshot」。
2. **Transport 状态机不得搬进 Domain**。把 busy/running 加进业务 HostSignal 并在几十处维护
   if/else（cache.md §16 被拒方案）——transport 状态机不是领域事实。业务只见 typed `HostSignal`
   （idle/retry/aborted/deleted），且信号只唤醒、不携带事实（`HostSignal.fs` 头注释：
   FALLBACK-003「Host signals wake, they do not carry facts」）。
3. **能力缺口必须可证明**。`HostContractUnsupported` 是显式失败：compaction 预防层关不掉 →
   启动失败；HOST-025 定位 canary 不能唯一 → membrane 禁止上线；HOST-019/024 任一不成立 →
   membrane fail closed。默默依赖 undocumented API = 生产里第一个炸。
4. **物理身份可信取得**。Transform→ProviderRunIdentity 用因果读（唯一未完成 assistant）而非
   same-root 猜测；命中 0/≥2 宁可放弃 seal。Tool 身份双半边（ToolContext 有 message+call id，
   before/after 只有 call id），缺一 fail closed（HOST-011）。猜 = 假绿。
5. **多实例按 directory 分叉是现实**。跨实例共享的只能是身份注册表（SessionParents /
   VerdictSessions），不能是 Journal writer——实测第二实例读不到主实例 verdict（why/host.md §3/§7）。
6. **不修改 OpenCode 本体**（ARCH-003）。只挂现有 Hook/SDK；修改 Host core = 每次升级维护一个
   fork，且与 upstream 契约脱节。
7. **reasoning 不是 visible text 的替身**。HOST-016 只负责把空 content 变成结构合法的非空
   content；把 reasoning/thinking 原文复制进 synthetic text 会改变 provider transcript 语义，甚至把
   仅属于模型内部通道的内容伪装成可见 assistant 文本。需要结构占位时只发送无语义 `"."`。

## RED 是什么样

```text
RED = 产品语义需要猜 Host private/streaming state 或依赖未经验证的物理能力。
```

具体症状：

- 业务层有 `message.updated` / `part.delta` 处理 → RED（HOST-001/ARCH-002）。
- 从 idle payload 推断 terminal / 完成 / 失败 → RED。
- `session.error = ProviderError` 被当成 `AttemptAborted`（或反之）→ RED（HOST-002 分型）。
- 用 callID 与别处 messageID 猜配对 / 使用 SDK 不存在的字段 → RED（HOST-011）。
- compaction 关不掉仍继续跑 → RED（HOST-006 prevention 未证明，必须 `HostContractUnsupported`）。
- reasoning sensor 从 visible text / tool output 触发 → RED（HOST-027）。

## 边界（DOES NOT OWN）

- OpenCode hook 名 / 参数 shape（upstream 实现细节；adapter 内部）。
- session ontology / lifecycle（→ `session-ontology` / `managed-session-lifecycle`）。
- provider language、interaction authority、projection（→ 各自包）。
- Pair guidance、Todo membrane、compaction policy 等 feature 语义（→ prefix-stability /
  obligation-ledger / context-compression 等）。
- upstream workaround/quirk（→ 各 feature owner 或 HOW）。
- Host 假设需要什么 proof 强度（→ `verification-system` 横向治理，非本包语义依赖）。
- QuiescencePermit 的 idle 资格语义（→ `causal-wait`）；本包只拥有观察 machinery。

## Independent Change Test

迁到另一 Host：只要 adapter 提供同等 capability（snapshot / coarse wake / transform / tool /
session API / identity observation），participant/mission/durability WHAT 不变（boundary card）。
