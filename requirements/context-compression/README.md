# context-compression

> 当历史过长时，只能以受控、证据边界明确的 semantic memory 替代可压缩部分。

## 一句话 WHY

provider history 可能超过可用上下文。压缩若靠猜模型窗口、先提交再回滚、或按错误文字分类，
会把模型容量猜测与未发生世界写进产品事实。

## WHAT 概览（→ WHAT.md）

| 组 | 命题 | 保证 |
|---|---|---|
| 失败驱动 | CONTEXT-COMPRESSION-001/002/005 | 不观察容量、不预测溢出、失败不分类 |
| 输入合同 | CONTEXT-COMPRESSION-003/004 | 200 KiB delta 合同；输出预算属 provider |
| 恢复槽 | CONTEXT-COMPRESSION-006/007/008 | armed∧primed∧hasMaterial；RequestKind 分派；X 不发压缩请求 |
| 候选诚实 | CONTEXT-COMPRESSION-009/010/011 | 候选未提交不是事实；选择严格新于已提交；提交语义分型 |
| 压缩材料 | CONTEXT-COMPRESSION-012/013/014 | delta TOML 合同；诊断不是控制输入；只覆盖本 X frames |
| 证据边界 | CONTEXT-COMPRESSION-015/016/017 | busy 不推进 coverage；Y 只物化 PrefixCoverage 完整 turn；Opening floor |

## HOW 概览（→ HOW.md）

- 类型：`Domain/PrefixCandidate.fs`、`Domain/PrefixProbeSelection.fs`、`Domain/BloggerDelta.fs`、
  `Domain/BloggerRequestContext.fs`、`Domain/RecoverySlot.fs`、`Domain/HostCompactionPolicy.fs`
- 投影：`Context/Companion/Blogger/Projection.fs`（frames/squash/coverage）、`Context/Companion/Projection.fs`、
  `Context/Companion/Blogger/ContextFactFold.fs`
- 接线：`Session/{Companion,CompanionHost,BloggerCoordinator,CompanionHostBlogger}.fs`

## proof 概览（→ PROOF.md）

- MOVE（已执行 Wave 2a）：`tests/unit/context/{blog-projection,companion-projection,blogger-delta,probe-selection,recovery-slot,host-compaction-policy,ctx014,terminal-validity}.test.mjs`（8 文件）
- REUSE：`tests/unit/context/{synthetic-toml,blogger-toml}.test.mjs`（TOML 渲染 → provider-projection，已迁）
  `requirements/durable-events/tests/fold-context-recovery.test.mjs`（fold → durable-events）、`requirements/context-compression/tests/**`（blogger 收敛，已迁）
- NEW：`ctx-capacity-observation-forbidden.test.mjs`（CTX-001）、`ctx-opening-floor.test.mjs`（CTX-016）

## 阅读顺序

1. `WHY.md` → 2. `WHAT.md` → 3. `HOW.md` → 4. `PROOF.md`

## DEPENDS ON

- `semantic-trace`：delta 与 gap 同源 XTrace；ingest cursor 是 XTrace 游标。
- `provider-projection`：TOML 渲染与 prefix 投影是 provider 表示（本包拥有「何时/哪些有资格」）。

## 边界（DOES NOT OWN）

- XTrace source facts → `semantic-trace`
- prefix byte-stability law / epoch → `prefix-stability`
- provider renderer → `provider-projection`
- Companion/Blogger 的 summarization Persona → `cognitive-environment` / `session-ontology`
- armed/primed（fallback 失败预算）→ `provider-attempt-recovery`
- 当前 200 KiB 常数是否永久：只有被证明是产品合同的上界才进入 WHAT（当前 CTX-003 是合同）
