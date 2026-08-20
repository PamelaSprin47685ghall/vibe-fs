# provider-projection — HOW

## 架构与实现机制

1. **三层装配架构**：
   - **Effectful Coordinator**：提取宿主只读上下文，构造不可变 `ProjectionSnapshot`（包含当前投影、已提交前缀、博客帧与传输消息）。
   - **Pure Projection Planner**（`ProjectionPlanner.fs`）：将各模块声明的 `ProjectionIntent` 按 canonical rank 排序，执行冲突判定（`reduce*` 系列），产出无歧义的意图序列。
   - **Canonical Renderer**（`ProjectionRenderer.fs`）：将意图序列折叠到语义树上，生成最终的 Wire 字节。

2. **语义视图与 Wire 视图分离**：
   - `ProviderSemanticProjection` 剥离易失传输元数据，提供跨会话一致的语义等价视图，作为 `CanonicalDigest` 计算的唯一输入。
   - `ProviderWireProjection` 在语义视图之上补充合成 ID 与本地时间线标记，服务于前缀缓存与物理传输。

3. **SyntheticToml 规范渲染**：
   - 统一管理换行规范化（CRLF → LF）、字符串转义、值树编码与注释排版。
   - 故意不提供业务解析器，确保单向渲染安全。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| PROVIDER-PROJECTION-001 | `requirements/provider-projection/tests/projection-algebra.test.mjs` |
| PROVIDER-PROJECTION-002 | `requirements/provider-projection/tests/projection-algebra.test.mjs` |
| PROVIDER-PROJECTION-003 | `requirements/provider-projection/tests/projection.test.mjs` |
| PROVIDER-PROJECTION-004 | `requirements/provider-projection/tests/projection-algebra.test.mjs` |
| PROVIDER-PROJECTION-005 | `requirements/provider-projection/tests/projection-algebra.test.mjs` |
| PROVIDER-PROJECTION-006 | `requirements/provider-projection/tests/projection-algebra.test.mjs` |
| PROVIDER-PROJECTION-007 | `requirements/provider-projection/tests/projection-algebra.test.mjs` |
| PROVIDER-PROJECTION-008 | `requirements/provider-projection/tests/synthetic-toml.test.mjs` |
| PROVIDER-PROJECTION-009 | `requirements/provider-projection/tests/join-result-renderer-entry-comment.test.mjs` |
| PROVIDER-PROJECTION-010 | `requirements/provider-projection/tests/pair-thought-transform.test.mjs` |
| PROVIDER-PROJECTION-011 | `requirements/provider-projection/tests/projection-algebra.test.mjs` |
| PROVIDER-PROJECTION-012 | `requirements/provider-projection/tests/synthetic-toml.test.mjs` |
