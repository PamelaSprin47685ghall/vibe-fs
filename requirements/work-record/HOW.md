# work-record — HOW

## 架构与核心机制

LifecycleWorkRecord（LWR）提供跨边界传递的单一结构化工作记录：
- **Opening**：记录初始委托与约束（支持 BlindPlan constitutive material）。
- **Chronicle**：已由压缩机制沉淀的 frame 列表。
- **Recent work**：压缩游标之后的原始未覆盖 suffix，其中最后一条助手文本作为正式陈述。

### 物化机制

1. **Full-lifecycle 物化**：从全局快照与 XTrace 游标物化完整生命周期记录。
2. **Bounded 物化**：根据指定的 `BoundedRange`（`[StartInclusive, EndExclusive)`）过滤 frames 与 trace 范围，计算局部 coverage，并默认将 `includeOpening` 置为 false。
3. **分向投影控制**：通过 `includeOpening` 参数控制是否在最终 Markdown 中渲染 Opening 节，段落为空时整段省略，段落标识由外层 wire 注入。

## 依赖关系

DEPENDS ON:
- `semantic-trace`
- `context-compression`
- `participant-horizon`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| WORK-RECORD-001 | `requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-002 | `requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs` |
| WORK-RECORD-003 | `requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-004 | `requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs` |
| WORK-RECORD-005 | `requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-006 | `requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-007 | `requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-008 | `requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-009 | `requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-010 | `requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-011 | `requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-012 | `requirements/work-record/tests/lwr-prose-claim-no-schema.test.mjs` |
| WORK-RECORD-013 | `requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-014 | `requirements/work-record/tests/lwr-record-coverage-vs-prefix-coverage.test.mjs` |
| WORK-RECORD-015 | `requirements/work-record/tests/lifecycle-work-record.test.mjs` |
| WORK-RECORD-016 | `requirements/work-record/tests/lifecycle-work-record-bounded.test.mjs` |
