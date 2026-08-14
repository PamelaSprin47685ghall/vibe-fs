# HOW — provider-projection

> 非 normative。描述实现模型与约束；真实规范见 `WHAT.md`。

## 实现模型

### 三层装配（PROJ-004）

```text
Effectful Coordinator
    Host 读取 → 完整 snapshot（ProjectionSnapshot，不可变）→ 交 Planner

Pure Projection Planner（Domain/ProjectionPlanner.fs）
    汇总各功能 ProjectionIntent → canonical rank 排序 → 冲突检查（reduce* 族）→ 有序 intent 序列

Canonical Renderer（Domain/ProjectionRenderer.fs + CompanionProjectionBuilder.fs）
    逐 intent 落语义树 → 渲染 provider wire bytes → digest/seal
```

- 禁令落点：功能模块只声明 intent（`Domain/ProjectionIntent.fs` 的 `ProjectionIntent`
  union），不得直接接收/改写 `Message list`；渲染器不驱动生命周期。
- `ProjectionSnapshot`（`Domain/ProjectionIntent.fs`）字段：`CurrentProjection` /
  `CommittedPrefix` / `BlogFrames` / `TransportMessages` / `HostReanchor`——attempt-local
  只读输入。

### Intent 排序与冲突（PROJ-005/006）

- 九种 intent：`KeepPhysicalPrefix` / `ActivatePrefixEpoch` / `InsertBlogFrames` /
  `InsertRepair` / `UseStrengthMirror` / `InsertStrengthFrames` / `SuppressTransportOnly`
  / `AppendReviewChallenge` / `ReanchorAfterCompaction`。
- `ProjectionPlanner`：同锚 intent 先按稳定总序归一化（canonical rank），再显式合并
  （幂等类 intent 合并）或返回 `ProjectionConflict`（`ConflictingPrefixSelection` /
  `ConflictingPrefixLifecycle` / `ConflictingBlogFrames` / `ConflictingRepair` /
  `ConflictingReviewChallenge` / Strength 专属冲突族）。**禁止依赖模块注册顺序**。
- 合并性质：重放型幂等；有序追加型保 canonical order；未定义组合 fail-closed。
- HOST-013 pair marker 不占 intent：`PairProgrammingThoughtTransform` 在 raw 域按
  durable gap anchor replay（wire 级无消息地址）。

### 语义树与两投影（PROJ-003 / VERIFY-007）

```text
ProviderSemanticProjection    // 去 ID：语义等价、跨会话可比较、canonical digest 唯一来源
ProviderWireProjection        // 含 ID、字节相等、本地时间线、seal 与前缀缓存用
```

- Semantic：过滤 transport-only 字段（timestamp/cost/usage/runtimeId…），TEXT 序列化
  顺序固定，供 digest。
- Wire：在 Semantic 之上补合成 identity（COMPANION-013）与本地时间线序号。
- 禁止：Wire 反解析回 Semantic 当 digest；用 wire shape 判语义等价。

### Canonical digest

```text
CanonicalDigest = SHA-256(规范序列化(ProviderSemanticProjection(tree)))
```

规范序列化：固定字段序、UTF-8、无空白歧义；同语义跨 ID 同一 digest。使用点：
CoveredPrefixDigest（失配 fail-closed）、canary `LegacyDigest = DslDigest` 切换判据、
Review 双 PERFECT（消费点归各 owner）。

### SyntheticToml（ARCH-010，唯一写法 owner）

- `Domain/SyntheticToml.fs`：`normalizeNewlines`（CRLF/CR→LF）、`escapeBasic` /
  literal-safe 判定（`'''` 零处理、closing delimiter 独占一行）、`renderString` /
  `comment` / `field` / `tableEntry` / `tableArrayEntry` / 值树 `encodeData` /
  `encodeFs` / `document` / `byteCount`（UTF-8）。
- **无 parser**：`There is deliberately no parser`——业务不得读回渲染文本；测试用外部
  parser（smol-toml）做 parseability round-trip。
- js-tools 值树（js-tools-toml-result）：成功对象 → `[data]`；磁盘效果 → 文末 `[fs]`；
  失败 → `# failed` + `code`/`reason`，无 `[data]`；禁止 JSON-in-string、禁止 `status`
  根级 discriminator、禁止私有 TOML 方言。

### instruction/data plane（corrective.md §1）

- 判据：owner 问「这一次投影，接收 agent 应把这段内容当作行动/认知指导（→顶层 `#`
  comment），还是当作结构化数据读取（→ field/table/value）」。
- 非法判据：trusted/untrusted、current/historical、来源方、祈使句外形。
- 已正确 surface 清单（禁止回退）：FinalityPrompt rejected/blessed → comment blocks；
  Join completed work_record → entry-local comment；ForkChildPayload parent_work_record →
  data；ReviewChallenge → comment-only；RuntimeNudge.* → document。

## 失败路径

- 同锚互斥 intent 同批出现 → `ProjectionConflict`（PROVIDER-PROJECTION-006 RED）。
- 输入排列改变投影或冲突结局 → 排列无关性测试红（006）。
- `SyntheticToml` 出现 parse/decode/read 导出 → no-parser 断言红（008/010）。
- 业务从结果 TOML 反解析控制流 → 违反 010（无直接门禁；no-parser 是结构性守卫）。
- digest 从 wire 反算 → 与 Semantic digest 失配测试红（011）。

## 历史与弃权

| 源 | 判定 | 说明 / 落点 |
|---|---|---|
| `changes/completed/projection-algebra-gap.md` | EVIDENCE | PROJ-008 迁移闭环：八 intent + Planner Canonical Rank + Renderer fold；`SuppressTransportOnly` 仅 Domain 骨架（生产 `TransportMessages` 恒空）；`replaceMessagesInPlace` 保留为 Host 适配写回原语。落点：WHY.md 失败模式 + WHAT 001/005 + 本 HOW |
| `changes/completed/js-tools-toml-result.md` | EVIDENCE | SyntheticToml 值树能力；两份文档（`# failed`+code/reason；`# ok`+`[data]`/`[fs]`）；被拒方向（JSON-in-string、status 信封、从结果 TOML 反解析控制流）。落点：WHAT 008/009/010 + 本 HOW |
| `changes/completed/cursor-pair-hint.md` | EVIDENCE（authority firewall 部分） | synthetic role 不产生 HumanRoot/Opening/completion/evidence；single semantic owner（不 tune wording per mode）。落点：WHAT 010 |
| `changes/completed/corrective.md` | EVIDENCE | instruction/data plane 判据；显式采用安全边界；已正确 surface 清单。落点：WHAT 009 |
| `changes/completed/cache.md` | 弃权（anchored prefix 部分） | HOST-013 anchored prefix / gap anchor / replay 属 `prefix-stability`（任务边界明确不重复收）；本包只收 renderer 侧（WHAT 005/010 的 wire 机制） |
| `docs/why/synthetic-toml.md` | EVIDENCE | 字符串写法唯一 owner；同 semantic input 同 bytes。落点：WHAT 008/012 |
| PROJ-008 迁移日程（Batch 1–6 顺序） | HOW | 迁移执行记录，非永久命题 |
| `NUL+BOM`、`auto-injected` 工具名、`source="pair-programming-auto-injected"` | HOW | wire 分隔符与工具名机制（COVERAGE HOST-013 HOW 行） |
| LegacyProjection 删除 / `LegacyDigest = DslDigest` 切换 | HOW | 迁移期机制 |
| `MaxKeywords=8`/`TopK=4` 等 tuning 值 | GARBAGE 弃权 | 不在本包命题内（归 knowledge-reuse/repository-investigation 的 HOW） |
