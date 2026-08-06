# Companion — 目标实现

## 需求意图与范围（A2 需求意图）

### 1. 问题陈述
在长对话与复杂任务推进中，主工作会话（WorkSession X）的原生 Transcript 会随着大量工具调用与中间推理快速膨胀。如果直接将所有中间细节保存在主会话中，会导致前缀缓存高频失效与上下文溢出。Companion 子模块旨在为每个 WorkSession X 配备且仅配备一个专属的 Sidecar Blogger 会话（CompanionSession Y），专门记录稠密、可压缩的 `BlogFrame` 工作日志，并与主会话的历史渲染彻底解耦。

### 2. 输入输出与规则边界
- **输入**：WorkSession X 产生的新材料（Material）、Blogger System Prompt、`blog` 工具提交。
- **输出**：`BlogFrame` 结构（`Entry` | `Squash`）、`BlogFrame` 投影序列、低信任上下文呈现。
- **核心边界与不变量**：
  1. 关联依 Session 种类决定（COMPANION-001/002）：每个 WorkSession 恰好一个 CompanionSession，Y 严禁递归（CompanionSession 的 BloggerSessionId 为 None）。
  2. 仅从 Durable Frames 重建（COMPANION-005）：Provider-visible 历史严格由持久化的 `BlogFrame` 序列与 typed 上下文重建，绝对禁止直接抓取/追加原始物理 Transcript。
  3. Coverage 严格分型（COMPANION-003）：`RecordCoverage`（管 LWR 缺口）与 `PrefixCoverage`（管前缀证明）不得混用。
  4. 事实驱动 Epoch 切换（COMPANION-009）：Epoch 切换仅允许由已提交的 Probe 提升或 Compaction 重锚事实驱动，绝对禁止根据 Token 容量估算切换（CTX-001）。

---

## COMPANION-005：BlogFrame 投影

```fsharp
type BlogFrameKind = Entry | Squash
type BlogFrame = { Kind: BlogFrameKind; Digest: string; TextRef: BlobRef }  // BlobRef 见 PERSIST-007
```

无 Seed。frame 正文是纯工作记录；`[[do_not_exec]] historic_frame` 是消息层渲染，不进 TextRef/digest/delta。

### 正常请求形状

```text
[system: blogger-system.md]
[assistant: historic_frame × N]     // 有 frames 时
[user: instruction header + blank + [[new_work_to_record]] data]  // 必须最后
>>> assistant: blog 恰好一次
```

instruction header 在 projection 时加上，不进 200 KiB chunk、不进 frame blob（CTX-013 data-only 冻结）。  
recent tips：低信任 previous_enforcer_tip 块（ENFORCER-071），不伪装 parent instruction。

### squash 形状

```text
system + 前 k 个 historic_frame + squash instruction（最后 user）
```

不含当前 delta、后半 frames、旧 tool、物理 transcript。

### 硬约束

1. 一逻辑轮次一次 `prompt_async`（transform 内拼成单 turn）。  
2. 历史只从 durable effective frames + typed request context 重建，禁止 raw transcript append。  
3. 最后一条 user 供 HOST-010 绑定 parentID。  
4. B 可见正文 = blog `text`，不含 Y 的 user/reasoning。

System 唯一加载：`resources/prompts/blogger-system.md`（managed-agent config）。
