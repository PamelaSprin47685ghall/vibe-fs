# Companion — 理由

每个 Work Session 配叶子 Y，是为了把「可压缩的工作日志」从主会话原始历史中分离，而不把 Companion 做成角色特权。

LWR 自包含跨 Session hand-off；父 LWR 不作 child Seed，防止多代 fork 指数嵌套。

RecordCoverage 与 PrefixCoverage 分型，避免「Y 还没覆盖完就声称可替换 X 前缀」。同 epoch 前缀字节稳定，是 KV-cache 与 ReviewSeal 的共同前提；epoch 切换必须由已提交事实驱动，不能由 token 估算驱动。

## 备选与被拒

**Companion 形态：每 WS 配叶子 Y vs 角色特权。** 拒特权：与 Role/Tier/工具面无关（COMPANION-001/002）；把「可压缩工作日志」从主会话原始历史分离，而非给某角色加权限。

**LWR 衔接：自包含跨 Session vs 父 LWR 当 child Seed。** 拒 Seed：多代 fork 指数嵌套（COMPANION-003）。父 LWR 只是 child 输入 context，不复制 Opening/Seed。

**coverage：Record/Prefix 分型 vs 混用。** 拒混用：Y 未覆盖完就声称可替换 X 前缀（COMPANION-003）。RecordCoverage 管 LWR gap，PrefixCoverage 管 prefix 证明，不可互换。

**epoch 切换：已提交事实驱动 vs token 估算。** 拒估算：按容量切 epoch 破坏 seal/前缀稳定。仅 probe 提升与 compaction 重锚两源（COMPANION-009）。

**low-trust 注入：明确标记 context block vs 伪装指令。** 拒伪装：低信任片段（frozen prefix、enforcer tip、historic_frame）必须显式标记，防被当 system/human 指令（COMPANION-010）。
