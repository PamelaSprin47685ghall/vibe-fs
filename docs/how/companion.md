# Companion — 目标实现

## COMPANION-005：BlogFrame 投影

```fsharp
type BlogFrameKind = Entry | Squash
type BlogFrame = { Kind: BlogFrameKind; Digest: string; TextRef: BlobRef }
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
