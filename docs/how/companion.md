# Companion — 目标实现

## Implements

行为合同见 `what/companion.md`；本文件只描述 frame 投影、squash 和运行时协调算法。

## Ownership

会话、frame 与 writer 边界见 `shape/companion.md`。

---

## COMPANION-005：BlogFrame 投影

```fsharp
type BlogFrameKind = Entry | Squash
type BlogFrame = { Kind: BlogFrameKind; Digest: string; TextRef: BlobRef }  // BlobRef 见 PERSIST-007
```

无 Seed。frame 正文是纯工作记录；`[[do_not_exec]] historic_frame` 是消息层渲染，不进 TextRef/digest/delta。

### 正常请求形状

```text
[system: PromptResources Blogger composition]
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

System 唯一加载：`PromptResources.systemForRole` 的 Blogger 组合（Common Law → `resources/provider/role/blogger` → 继承 Library；managed-agent config）。

---

## OpeningMaterial / WorkRecord 物化（COMPANION-003/014/015）

所有权见 `shape/companion.md`。本节只写区间与投影算法。

### OpeningMaterial 区间（preserved）

```text
OpeningBoundary = WorkRecordStart
  // Life / XTrace Opening cursor 纯推导（TODO-001）
  // BlindPlan：含 T1 commitment call + canonical accepted result（TODO-015）

OpeningMaterial = XTrace.slice[workStart, OpeningBoundary)
  // exact preserved interval；禁止第二事实源重建
```

```text
XTrace.forOpening:
  keep constitutive commitment material（含 BlindPlan T1 call/result）

XTrace.forWorkRecordRecent:
  filter incidental raw tool traffic
```

禁止：`OpeningPromptRaw` / AssignmentText / AuthoritativeRequirements 拼接；重编号 requirements。  
Opening always raw：never Blogger / never Y / never prefix-replaced；survives compaction / reanchor / recovery。  
after Opening（WorkRecordStart）→ ordinary Chronicle / Recent / Y machinery。

### WorkRecord 三段

```text
materializeLWR(range, includeOpening):
  Opening        = if includeOpening then OpeningMaterial else omit
  Chronicle      = effective Y frames in range（BlogEntryCommitted）
  Recent work    = RawGapFromX（未覆盖 suffix；剔 raw tool；含最后一条助手文本）
  headings       = Opening / Chronicle / Recent work
  // 无 Closing report。Terminal 是私有完成标记，不渲染。
  // 旧标题 Opening task / Work log / Uncompressed tail / Final output 已删、无 alias
  // # 仅由 SyntheticToml.comment 在 wire 注入
  // inspect 答案 = bounded record 本身
```

禁止第二套 work-record renderer（TODO-008）。process / Finality 一律 request-range bounded。

### includeOpening

```text
parent → child:                 true
child → parent:                 false
same-session frozen prefix:     true
process / Finality / SyncDelegate caller: false
```

Canonical record **保留** Opening，即使投影省略（COMPANION-015）。