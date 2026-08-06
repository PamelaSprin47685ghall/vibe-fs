# 上下文恢复 — 目标实现

## CTX-010：attempt-local prefix probe

恢复槽中替换 X 前缀时，**不**立即改 ActivePrefixEpoch。候选只进不可变 `AttemptExecutionProfile.ProjectionChoice`。

```text
probe 成功 → 提升为 ActivePrefixEpoch（写 PrefixRebaseCommitted）
probe 失败 → 丢弃候选；后续非 probe 槽用旧 epoch
```

禁止先提交再回滚；故无 PrefixProbeRolledBack 类事实。  
`A′` 失败不禁止 `B′` 用等价候选重试。

投影形状：system + 低信任 companion memory + cutoff 后 raw X + 当前 physical user（最后）。

## CTX-011：候选选择

- 候选必须严格新于已提交 epoch 的 coverage 证明。  
- 无候选 → 不构造空 probe，走正常主请求。  
- CoverableTurnCutoff 只前进；失配 CoveredPrefixDigest → fail closed（COMPANION-011）。

## CTX-012：提交语义

| 动作 | 成功 | 失败 |
|------|------|------|
| X probe | 提交 epoch + SealRoot 继承 | 无事实 |
| Y squash | BlogSquashCommitted，FrameEpoch+1 | 不改 frames/coverage |

squash 选择范围/级联：前半有效 frames；不混父 LWR。

## CTX-013：Blogger delta TOML

- data-only TOML 冻结进 blob；instruction header 投影时加。  
- 硬上限 200 KiB 渲染后字节；超限确定性切块/截断策略保持可复现。  
- 含 decision-relevant host-visible reasoning；无 hidden reasoning 伪造。  
- 与 LWR gap 分投影，禁止混用 renderer 输出当 canonical digest。
