# prefix-stability — WHY

Provider 的 KV-cache 命中与模型的认知连续性均依赖于历史前缀的严格稳定。在同一 semantic epoch 内，如果已发送给 provider 的历史字节发生位置移动、重新渲染或标记重排，即使高层语义等价，底层物理字节的改变也会彻底击穿前缀缓存，造成印记断裂与认知失真。

**prefix-stability 保证：同一 semantic epoch 内已提交的前缀严格保持 byte-stable；冷边界切换只能由已提交的事实驱动，严禁由 Token 估算或临时状态驱动。**

## 核心不变量与张力

- **同 Epoch 字节单调追加（Append-Only Prefix Law）**：在同一 PrefixEpoch 内，后一次请求的 provider wire 必须是前一次请求的精确字节前缀。
- **冷边界事实驱动**：Epoch 切换仅允许由成功 probe 提升、Host compaction 重锚与 TodoCheckpoint lag-1 rebase 三种已提交证据源触发，且必须使 `EpochId += 1`。
- **Candidate 与 Committed 严格隔离**：未提交的候选前缀绝不能被视作已提交事实，probe 失败不得对已生效前缀产生任何副作用。

## 违反边界的失败意义

- 无业务语义变化时历史字节被重排或修改（破坏同 epoch 前缀稳定性）。
- 将未提交的候选前缀误当成稳定前缀。
- 冷边界由容量/Token 估算等临时状态触发而非由真实已提交事实触发。

## DEPENDS ON

- `provider-projection`
- `context-compression`
- `provider-language`
- `participant-identity`
