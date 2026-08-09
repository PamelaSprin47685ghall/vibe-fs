# Companion — 可观察行为

条款前缀：`COMPANION-`。  
关联与 epoch 边界见 `shape/companion.md`。  
Y 投影形状见 `how/companion.md`。  
失败驱动恢复见 `what/context.md`。

## COMPANION-001：每个 Work Session 都有 Companion

每个由万象术管理、能发普通 provider request 的 Session 是 Work Session（X），**恰好**一个叶子 Companion Blogger（Y）。

该关系**不是**角色资格：与 Role、Tier、工具面、公开/内部、Logical Run、Authority、Fallback 无关。  
禁止任何 eligibility 白名单或从 last message / cache 推断「该不该有 Y」。

## COMPANION-002：Companion 是叶子

```text
X → 恰好一个 Y
Y → 不再递归
深度恒为 1
```

## COMPANION-003：XTrace · Frames · LWR

| 概念 | 含义 |
|------|------|
| XTrace | X 唯一原始语义轨迹（HOST-005） |
| BlogFrame | Y 历史（Entry/Squash，无 Seed） |
| LWR | 跨 Session 唯一工作记录 |

```text
LWR(X) = OpeningPromptRaw?
       + CompressedMiddleFromY（全部有效 frame）
       + RawGapFromX（未覆盖 suffix，经 LWR 投影）
       + TerminalOutputRaw
```

Opening 永不送 Y 压缩；Terminal 不复经 transform。  
LWR **硬禁止** raw tool call/result 与 call/result linkage。  
父 LWR 是 child 输入 context，**不**作 child Seed / Opening 复制。

两种 coverage 禁止混用：

```text
RecordCoverage   // XTrace 游标；LWR gap 起点；可落在 turn 中间
PrefixCoverage   // 完整 Host turn 边界；prefix replacement 证明
```

X prefix probe 只能用 CoverableRecordPrefix（Opening + 可证明完整 turn 的 Y prefix），不能用 RawGap。

`includeOpening`：父→子 true；子→父 false；同 Session frozen prefix true。

## COMPANION-004：Y 的 system

唯一来源：`resources/prompts/blogger-system.md`。  
fast/deep blogger 同 prompt、同 `{blog}` 工具面，只模型绑定不同。  
禁止在 prompt 插入动态 token/预算/窗口信息（CTX-001）。

## COMPANION-006：Frame squash

squash 只处理本 X 的 frames，不混父 context（动作细节 CTX-012）。

## COMPANION-007：Semantic 投影与 TOML delta

送 Y 的 delta 与 LWR gap 同源 XTrace、不同投影：delta 可含 tool 作压缩输入；LWR gap 剔除 raw tool。  
canonical digest 用 Semantic projection，禁止反向解析 TOML。

## COMPANION-008：Busy skip 与 coverage 不推进

Blogger busy：不打断、不排队、**不推进** RecordCoverage；失败/空/XML-only 不推进。  
仅 `BlogEntryCommitted` 原子推进 frame 可见性与 RecordCoverage（PERSIST-010）。  
Host compaction 只作废 PrefixCoverage 映射，不得清零 RecordCoverage/Frames。

## COMPANION-010：低信任注入

FrozenRecordPrefix 以明确标记的 context block 注入 X，不伪装人类/system 指令，同 epoch 内内容冻结。

## COMPANION-011：Cutoff 证明

Cutoff 只在完整 semantic turn 边界；投影前重算 CoveredPrefixDigest，失配 fail closed。  
digest 失配不是 compaction 善后手段——善后是 HOST-006 重锚。

## COMPANION-012：Provider-visible projection

缓存比较只用进模型的字段（排除 timestamp/cost/usage/runtimeId…）。

## COMPANION-013：Synthetic 稳定身份

Synthetic id 由 SealRoot / frameEpoch / ordinal 等确定性派生；禁止 GUID/random/时间/Host runtimeId。  
probe 成功 promote 时继承同一 SealRoot，避免多余冷边界。
