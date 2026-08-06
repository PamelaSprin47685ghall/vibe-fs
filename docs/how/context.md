# 上下文恢复 — 目标实现

## 需求意图与范围（A2 需求意图）

### 1. 问题陈述
在长对话或多轮工具调用中，模型上下文窗口可能发生溢出。传统方案依赖 Token 估算与预测式压缩，但估算值极易受模型/分词器影响漂移，且在失败前压缩会提前毁坏 KV-Cache 前缀稳定性。上下文恢复模块必须在绝对禁止 Token 估算（CTX-001）与绝对禁止失败前压缩（CTX-002）的硬性约束下，完全由真实 Physical Attempt 失败驱动恢复，通过 Attempt-local Prefix Probe 与 Y-Squash 重新定位有效的 Prefix Epoch。

### 2. 输入输出与规则边界
- **输入**：Reconciler 确认的真实物理失败（Outcome = Failed）、待探针测量的 X 前缀候选、Blogger 未压缩 Blog 帧。
- **输出**：`PrefixRebaseCommitted` / `BlogSquashCommitted` 事实，以及彼此独立的 `ActivePrefixEpoch` / `FrameEpoch` 投影。
- **核心边界与不变量**：
  1. 失败驱动（CTX-001/002）：恢复动作的前提是一次真实物理失败；绝对禁止在失败前主动压缩或估算 Token 窗口。
  2. 尝试局域化（CTX-010）：X Probe 候选仅写入 Attempt-local 的 `ProjectionChoice`；Probe 成功才提交写盘，Probe 失败丢弃候选且**绝对不写恢复/回滚事实**。
  3. 失败不分类（CTX-005）：不按 Provider 错误散文文字分叉，统一视作 Outcome = Failed 驱动。

---

## CTX-010：attempt-local prefix probe

恢复槽中替换 X 前缀时，**不**立即改 ActivePrefixEpoch。候选只进不可变 `AttemptExecutionProfile.ProjectionChoice`。

```text
probe 成功 → 提升为 ActivePrefixEpoch（写 PrefixRebaseCommitted）
probe 失败 → 丢弃候选；后续非 probe 槽用旧 epoch
```

禁止先提交再回滚；故无 PrefixProbeRolledBack 类事实。  
`A′` 失败不禁止 `B′` 用等价候选重试。

投影形状：system + 低信任 companion memory + cutoff 后 raw X + 当前 physical user（最后）。

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

## CTX-013：Blogger delta TOML

- data-only TOML 冻结进 blob；instruction header 投影时加。  
- 硬上限 200 KiB 渲染后字节；超限确定性切块/截断策略保持可复现。  
- 含 decision-relevant host-visible reasoning；无 hidden reasoning 伪造。  
- 与 LWR gap 分投影，禁止混用 renderer 输出当 canonical digest。
