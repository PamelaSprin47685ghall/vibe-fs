# semantic-trace — HOW

## 架构机制

### XTrace 核心模型与单调游标

1. **游标与半开区间**：`XTraceCursor` 维护全局单调递增的序列号。提供 `sliceBetween [start, endExclusive)` 与 `sliceFrom [start, head)` 支持对历史前沿的精确半开切片。
2. **同源多投影**：
   - `forOpening`：保留初始任务及宪章性承诺，作为 OpeningMaterial 永久封存；
   - `forWorkRecord`：过滤原始工具调用及结果，保留正文与推理，用于物化 LifecycleWorkRecord；
   - `flatten`：将消息与部件平铺为带角色的标准语义流，供下游增量记忆（Blogger）消费。

### 捕获管线与重锚持久化

- **幂等捕获与 Provenance 分段**：`XTraceCapture` 以物理 `host-part-id` 结合消息与运行标识构造溯源标识 `g:N/msg:<id>/host-part:<id>`，防止数组下标偏移造成重复录入。
- **Compaction 隔离**：宿主触发 `ContextReanchored` 时仅重置物理前缀纪元，XTrace 的已持久化部件、Opening 记录与 `RecordCoverage` 保持完全存活，保证因果历史不发生丢失。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| SEMANTIC-TRACE-001 | `requirements/semantic-trace/tests/x-trace-fold.test.mjs` |
| SEMANTIC-TRACE-002 | `requirements/semantic-trace/tests/x-trace-capture.test.mjs` |
| SEMANTIC-TRACE-003 | `requirements/semantic-trace/tests/x-trace.test.mjs` |
| SEMANTIC-TRACE-004 | `requirements/semantic-trace/tests/x-trace-provider-run-provenance.test.mjs` |
| SEMANTIC-TRACE-005 | `requirements/semantic-trace/tests/x-trace.test.mjs` |
| SEMANTIC-TRACE-006 | `requirements/semantic-trace/tests/x-trace.test.mjs` |
| SEMANTIC-TRACE-007 | `requirements/semantic-trace/tests/x-trace.test.mjs` |
| SEMANTIC-TRACE-008 | `requirements/semantic-trace/tests/x-trace-capture-boundary.test.mjs` |
| SEMANTIC-TRACE-009 | `requirements/semantic-trace/tests/x-trace-compaction-survival.test.mjs` |
| SEMANTIC-TRACE-010 | `requirements/semantic-trace/tests/x-trace-capture-hardening.test.mjs` |
