# requirement-grounding — HOW

## 架构模型与执行流

`requirement-grounding` 通过目录发现、路径模式匹配与统一 file-access observation 实现全生命周期的规范接入。它是 provider knowledge sidecar，不是文件 effect gate：

```text
文件访问事实 (native read/mutation 或 js-* read/effect)
  ↓
路径解析器 Scope Resolver (self coverage 优先，APPLIES-TO wildmatch 求值)
  ↓
命中 Requirement Packages 集合 (按名称排序)
  ↓
Materializer (提取包根目录 *.md，计算 content digest)
  ↓
Visible-Material Fold (登记执行者实际读到的 material path + content digest)
  ↓
Missing-Material Filter (同 horizon 已看见的材料逐文件跳过)
  ↓
自动补读剩余材料 (ordinary: 普通 read；Cursor: NUL+BOM path-attributed suffix)

原 mutation / js transaction 始终按自身语义继续；grounding 失败不得转换成 tool failure。
```

## 核心机制

### 1. 范围解析与材料物化 (Scope Resolution & Materialization)

- **解析逻辑**：`requirements/<package>/**` 内部路径无条件触发本包 self coverage；包外路径按 `<package>/APPLIES-TO` 声明的 glob 规则求值。
- **规范材料过滤**：统一仅提取 `requirements/<package>/` 根级直接存在的 `*.md` 文件（按文件名升序排列），过滤掉 `tests/**` 与子目录及 `APPLIES-TO` 元数据，防止实现细节与测试代码污染上下文。
- **双层摘要**：package snapshot 继续保留稳定 digest 以冻结自动 occurrence；每个 material 同时拥有 `path + content` 版本身份，作为 horizon 去重的最小单位。未变化材料不会因为兄弟文档变化而重复注入。

### 2. 统一文件访问观察 (File-access Observation)

- **native read**：`tool.execute.after` 登记成功 read 的路径；若该路径本身是 package 根级 Markdown，先记为 visible material，再解析其覆盖 package 并只请求未读兄弟材料。
- **native mutation**：`tool.execute.before` 仅做 fail-open grounding request，以便尽可能在 effect 前冻结当前规范；无论 request 成败都不拒绝、不延期原工具。
- **js-* read**：sandbox 的 `js.read` 成功后把实际读取路径记录到本次 file-access observation；内部为了 `edit` staging 而读取旧文本不算模型读入。
- **js-* effect**：transaction 在 preflight 后把显式 read set 与完整 mutation effect set 一并交给同一 observation port；该 port 的错误被吞掉，commit 资格只由 repository-programming 自身事务规则决定。
- **顺序**：同一 js-* 调用先登记 read-visible facts，再解析 effect coverage，因此“先读 WHAT.md、再改受覆盖源码”不会把 WHAT.md 自动重复注入。

### 3. 持久化与前缀保护 (Durable Projection & Prefix Stability)

- 规范读取产生带类型的持久化 occurrence，固定记录调用参数与原始结果字节。
- 自动规范读取产生带类型的持久化 occurrence；执行者主动读取 grounding material 产生独立的 visible-material 事实。两者共同驱动去重，但只有自动 occurrence 参与 synthetic replay，避免把已经存在于真实 transcript 的主动 read 再伪造一遍。
- 同一 horizon 内的后续轮次直接重放已冻结的自动 occurrence，严禁重新扫描历史 occurrence 对应文件，以确保 provider KV 缓存前缀字节严格不变。
- 发生上下文重锚 (`ContextReanchored`) 时仅重置内存中的已接入集合，保留历史 occurrence，在后续触碰时作为新事件追加在 wire 尾部。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| REQUIREMENT-GROUNDING-001 | `requirements/requirement-grounding/tests/scope-resolution.test.mjs::WHAT[REQUIREMENT-GROUNDING-001] discovers requirement packages from the current workspace without a Wanxiangshu package list` |
| REQUIREMENT-GROUNDING-002 | `requirements/requirement-grounding/tests/scope-resolution.test.mjs::WHAT[REQUIREMENT-GROUNDING-002] treats a package own requirements subtree as implicit coverage that APPLIES-TO cannot cancel` |
| REQUIREMENT-GROUNDING-003 | `requirements/requirement-grounding/tests/scope-resolution.test.mjs::WHAT[REQUIREMENT-GROUNDING-003] evaluates APPLIES-TO as ordered positive wildmatch includes with bang exclusions` |
| REQUIREMENT-GROUNDING-004 | `requirements/requirement-grounding/tests/scope-resolution.test.mjs::WHAT[REQUIREMENT-GROUNDING-004] returns every overlapping package in deterministic package-name order` |
| REQUIREMENT-GROUNDING-005 | `requirements/requirement-grounding/tests/grounding-delivery.test.mjs::WHAT[REQUIREMENT-GROUNDING-005] APPLIES-TO external grounding injects only direct Markdown and excludes tests plus the manifest` |
| REQUIREMENT-GROUNDING-006 | `requirements/requirement-grounding/tests/grounding-delivery.test.mjs::WHAT[REQUIREMENT-GROUNDING-006] direct Markdown read counts as visible grounding material and only unread siblings are injected`；`requirements/requirement-grounding/tests/grounding-delivery.test.mjs::WHAT[REQUIREMENT-GROUNDING-006] deduplicates material content versions and re-grounds only the changed Markdown sibling`；`requirements/requirement-grounding/tests/grounding-delivery.test.mjs::WHAT[REQUIREMENT-GROUNDING-006] reanchor_resets_horizon_coverage_so_the_same_digest_must_ground_again` |
| REQUIREMENT-GROUNDING-007 | `requirements/requirement-grounding/tests/opencode-gate.test.mjs::WHAT[REQUIREMENT-GROUNDING-007] ordinary providers replay anchored read call-result pairs while Cursor appends NUL-BOM result-only bytes after the pseudo-skill with stable source-path attributes`；`requirements/requirement-grounding/tests/opencode-gate.test.mjs::WHAT[REQUIREMENT-GROUNDING-007] grep match files do not trigger APPLIES-TO before an explicit read`；`requirements/requirement-grounding/tests/requirement-grounding-project-surface.test.mjs::WHAT[REQUIREMENT-GROUNDING-007] RequirementGroundingTransform owns projectOrTerminate entry point` |
| REQUIREMENT-GROUNDING-008 | `requirements/requirement-grounding/tests/opencode-gate.test.mjs::WHAT[REQUIREMENT-GROUNDING-008] mutation grounding is weak observation and never becomes tool admission` |
| REQUIREMENT-GROUNDING-009 | `requirements/requirement-grounding/tests/repository-programming-gate.test.mjs::WHAT[REQUIREMENT-GROUNDING-009] js-* mutations commit normally while grounding observes the full effect set without admission` |
| REQUIREMENT-GROUNDING-010 | `requirements/requirement-grounding/tests/repository-programming-gate.test.mjs::WHAT[REQUIREMENT-GROUNDING-010] js-* read is a real read: covered code triggers grounding and already-read Markdown is deduplicated` |
| REQUIREMENT-GROUNDING-011 | `requirements/requirement-grounding/tests/grounding-delivery.test.mjs::WHAT[REQUIREMENT-GROUNDING-011] ordinary read observations add knowledge without creating authority or expanding capability` |
| REQUIREMENT-GROUNDING-012 | `requirements/requirement-grounding/tests/grounding-delivery.test.mjs::WHAT[REQUIREMENT-GROUNDING-012] freezes ordinary read-pair bytes and Cursor path-attributed result bytes for restart replay while changed digests append without rewriting the provider prefix` |
