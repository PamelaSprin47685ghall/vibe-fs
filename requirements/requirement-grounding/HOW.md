# requirement-grounding — HOW

## 架构模型与执行流

`requirement-grounding` 通过目录发现、路径模式匹配与宿主钩子门禁实现全生命周期的规范接入：

```text
文件操作请求 (Read 或 Mutation)
  ↓
路径解析器 Scope Resolver (self coverage 优先，APPLIES-TO wildmatch 求值)
  ↓
命中 Requirement Packages 集合 (按名称排序)
  ↓
Materializer (提取包根目录 *.md，计算 content digest)
  ↓
Deduplication & Horizon Filter (若同 horizon 已存在相同 digest 则跳过)
  ↓
[Read 路径]: 注入普通 read tool call/result 观察 (Cursor 追加 NUL+BOM 携带 path 属性)
[Mutation 路径]: 检查准入；若存在未 grounding 包，拒绝当前写入，阻断副作用，先注入规范读取
```

## 核心机制

### 1. 范围解析与材料物化 (Scope Resolution & Materialization)

- **解析逻辑**：`requirements/<package>/**` 内部路径无条件触发本包 self coverage；包外路径按 `<package>/APPLIES-TO` 声明的 glob 规则求值。
- **规范材料过滤**：统一仅提取 `requirements/<package>/` 根级直接存在的 `*.md` 文件（按文件名升序排列），过滤掉 `tests/**` 与子目录及 `APPLIES-TO` 元数据，防止实现细节与测试代码污染上下文。
- **摘要指纹**：对所有材料内容按规范顺序计算稳定 digest，与工作区标识绑定为唯一 grounding identity。

### 2. 宿主钩子与拦截门禁 (Host Hooks & Mutation Gate)

- **读取拦截**：在 `tool.execute.after` 阶段捕获明确的读取路径，自动将相关规范材料作为普通 read 结果追加至当前交互视界。
- **写入拦截**：在 `tool.execute.before` 阶段验证目标路径。若缺失 grounding，中止实际文件修改并返回预期非致命的拦截响应，由宿主层先行补齐规范读取。
- **事务准入**：可编程事务在 staging 完成后、真实 commit 前对全部涉及路径进行联合解析，确保多文件修改整体满足规范准入。

### 3. 持久化与前缀保护 (Durable Projection & Prefix Stability)

- 规范读取产生带类型的持久化 occurrence，固定记录调用参数与原始结果字节。
- 同一 horizon 内的后续轮次直接重放固化数据，严禁重新扫描磁盘，以确保 provider KV 缓存前缀字节严格不变。
- 发生上下文重锚 (`ContextReanchored`) 时仅重置内存中的已接入集合，保留历史 occurrence，在后续触碰时作为新事件追加在 wire 尾部。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| REQUIREMENT-GROUNDING-001 | `requirements/requirement-grounding/tests/scope-resolution.test.mjs` |
| REQUIREMENT-GROUNDING-002 | `requirements/requirement-grounding/tests/scope-resolution.test.mjs` |
| REQUIREMENT-GROUNDING-003 | `requirements/requirement-grounding/tests/scope-resolution.test.mjs` |
| REQUIREMENT-GROUNDING-004 | `requirements/requirement-grounding/tests/scope-resolution.test.mjs` |
| REQUIREMENT-GROUNDING-005 | `requirements/requirement-grounding/tests/grounding-delivery.test.mjs` |
| REQUIREMENT-GROUNDING-006 | `requirements/requirement-grounding/tests/grounding-delivery.test.mjs` |
| REQUIREMENT-GROUNDING-007 | `requirements/requirement-grounding/tests/opencode-gate.test.mjs` |
| REQUIREMENT-GROUNDING-008 | `requirements/requirement-grounding/tests/opencode-gate.test.mjs` |
| REQUIREMENT-GROUNDING-009 | `requirements/requirement-grounding/tests/repository-programming-gate.test.mjs` |
| REQUIREMENT-GROUNDING-010 | `requirements/requirement-grounding/tests/repository-programming-gate.test.mjs` |
| REQUIREMENT-GROUNDING-011 | `requirements/requirement-grounding/tests/grounding-delivery.test.mjs` |
| REQUIREMENT-GROUNDING-012 | `requirements/requirement-grounding/tests/grounding-delivery.test.mjs` |
