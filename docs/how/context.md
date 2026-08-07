# 上下文恢复 — 目标实现

## Implements

行为合同见 `what/context.md`；本文件只描述 probe、squash、delta 与恢复槽算法。

## Ownership

恢复数据和端口边界见 `shape/context.md`。

---

## attempt-local prefix probe 机制（行为见 what/context.md CTX-010）

行为（不立即改 ActivePrefixEpoch、成功提升/失败丢弃、禁止先提交再回滚、A′ 失败不禁止 B′ 重试）权威定义见 `what/context.md` CTX-010。

本处只留机制：候选进不可变 `AttemptExecutionProfile.ProjectionChoice`；投影形状为 system + 低信任 companion memory + cutoff 后 raw X + 当前 physical user（最后）。

---

## CTX-011：候选选择

- 候选必须严格新于已提交 epoch 的 coverage 证明。  
- 无候选 → 不构造空 probe，走正常主请求。  
- CoverableTurnCutoff 只前进；失配 CoveredPrefixDigest → fail closed（COMPANION-011）。

---

## CTX-012：提交语义

| 动作 | 成功 | 失败 |
|------|------|------|
| X probe | 提交 epoch + SealRoot 继承 | 无事实 |
| Y squash | BlogSquashCommitted，FrameEpoch+1 | 不改 frames/coverage |

squash 选择范围/级联：前半有效 frames；不混父 LWR。

---

## Blogger delta TOML 机制（行为见 what/context.md CTX-013）

行为（data-only 冻结、200 KiB 硬上限、decision-relevant reasoning、与 LWR gap 分投影）权威定义见 `what/context.md` CTX-013。
