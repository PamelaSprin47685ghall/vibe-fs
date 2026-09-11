# institutional-learning — HOW

## 架构机制与核心模型

### 1. 私有 Enhancer 提炼流程

1. **调用模型与输入边界**：
   - `celebrate` 与 `regret` 作为领域入口，分别构造类型化的 `Experience(kind, text)` 输入；
   - 调度短生命周期的私有 Enhancer，传入当前会话绑定的 live Rulebook 快照与经验正文，限制其在受控上下文内提炼；
   - 输出纯代数结果：`Absorb(existingRule)`、`Birth(candidateRule)` 或 `Discard(reason)`。

2. **准入交互与规则合流**：
   - 对于 `Birth` 结果，调用 `behavior-diagnosis` 的纯准入预检接口，验证中英双语完整性、命名冲突及结构合法性；
   - 预检通过后获取携带预期 `RulebookRevision` 的准入事实凭证，由底层保证规则库一致性。

### 2. 原子事务与 Attention Closure

1. **Staging 与原子提交**：
   - 经验评估完成后，首先在内存中阶段化待写入事实；
   - 若为 `celebrate`，调用 `attention-regulation` 提取当前未弹出的 `DeferredWork` 列表；
   - 发起单笔原子持久化事务，同时提交：
     - `LearningDispositionCommitted(occurrenceId, frozenResult, disposition)`
     - `InstitutionalRuleBorn(...)`（仅 BIRTH 产生）
     - `DeferredWorkResurfaced(...)`（仅 celebrate 且存在暂缓项时产生）
   - 若发生预期 `RulebookRevision` 冲突，整笔事务放弃提交并允许一次重新评估。

2. **重放与结果冻结**：
   - 无论学习结果为 ABSORB、BIRTH 还是 DISCARD，均由 `LearningDispositionCommitted` 事实冻结其呈现文本；
   - 重放路径直接读取持久化事实返回，不重复调用 Enhancer、不重复写入规则、不重复弹出暂缓项。

## 编译边界

事实、Enhancer、projection 与既有 Surface 所在分片直接引用 `runtime-platform/digest`，另外仅依赖 Identity 与 Enforcer catalog；不再借助 `host-boundary/host-digest` 引入 OpenCode 消息和事件类型。`InstitutionalEnhancer.rulebookRevision` 的排序、字段分隔与 UTF-8 摘要输入未变；工具侧仍负责装配 live Rulebook、持久化与暂缓工作，不把这些效果移入纯分片。

在 `686f3a9c4` 上替换这一条引用后，正式 planner 的 forward closure 从 7 个项目／38 个 `.fs/.fsi` 输入收窄到 4 个项目／16 个输入。该分片真实 focused Fable 编译通过（54 parsed sources，fingerprint `a731717f814a`）；其新产物的公开 Surface revision smoke 覆盖空规则、单规则与 Unicode／CRLF 多规则输入。这里的闭包数量不包括编译器隐式输入，不是编译耗时改善的证据。

2026-09-12：`Enforcer/InstitutionalLearning/Fold.fs` 这个只把 `projection.InstitutionalLearning` 写回聚合的裸装配包装已删除，同批删除的还有 Fission、Concern、Attention 三个同类包装；装配现由 `Composition/Durable/ProjectionUpdate.applyInstitutionalLearning` 与 `applyAttentionLearning` 承担，调用顺序仍是「先写 `InstitutionalLearning`，再用同一事实 resurface attention」。本分片剩余文件 `InstitutionalLearningTools.fs` 仍真实读取聚合（`AgentProjection.pendingAttentionWorkPairs`、`snapshot.AgentProjections.*`），因此保留 `composition-durable-*` 引用，闭包仅从 158 收到 157 个 `.fs`；不要把它读成隔离成果。
真实 `InstitutionalLearningTools` consumer 与 Enhancer 签名反向 consumer 的影响集合经 `compile-impact` 合并为一次编译，通过 1432 parsed sources／1394 items（fingerprint `f4c0e60d84d5`）。该集合仍含其他 consumer 真正需要的 Host 合同，不能把局部分片的闭包缩小推广到全部反向 consumer。

## 验证与测试落点

INSTITUTIONAL-LEARNING-007 原本由一条源码文本断言「覆盖」：它 match `Composition/Durable/Fold.fs` 与工具文件的字符串，其中「`ExperienceKind.Celebrate -> AttentionProjection.pending`」甚至由注释满足。该断言不是行为证明，也没有 JS 可驱动的替代路径（没有暴露「折叠一条 learning 事实」的 Surface），因此 2026-09-12 连同 fold 装配迁移一并删除，不把新实现文本重新钉成断言；该条款当前没有可执行落点，已由 `node scripts/check.mjs` 的 requirement-trace 如实报告为 proof gap。切片侧语义仍由 `attention-regulation` 的 `pending`/`resurface` 测试与本包 008 的冻结语义覆盖。

| 命题 | 落点测试 |
|---|---|
| INSTITUTIONAL-LEARNING-001 | `requirements/institutional-learning/tests/institutional-learning.test.mjs::WHAT[INSTITUTIONAL-LEARNING-001] celebrate and regret accept one raw natural-language experience without a rule template` |
| INSTITUTIONAL-LEARNING-002 | `requirements/institutional-learning/tests/institutional-learning.test.mjs::WHAT[INSTITUTIONAL-LEARNING-002] one enhancer evaluation yields exactly one ABSORB BIRTH or DISCARD disposition with no score state` |
| INSTITUTIONAL-LEARNING-003 | `requirements/institutional-learning/tests/institutional-learning.test.mjs::WHAT[INSTITUTIONAL-LEARNING-003] enhancer is bounded to the supplied experience and live rulebook snapshot` |
| INSTITUTIONAL-LEARNING-004 | `requirements/institutional-learning/tests/institutional-learning.test.mjs::WHAT[INSTITUTIONAL-LEARNING-004] unsafe raw experience cannot bypass behavior-rule admission by directly birthing a rule` |
| INSTITUTIONAL-LEARNING-005 | `requirements/institutional-learning/tests/institutional-learning.test.mjs::WHAT[INSTITUTIONAL-LEARNING-005] no reusable trigger or nonduplicate mechanism degrades to DISCARD rather than attention-tax debt` |
| INSTITUTIONAL-LEARNING-006 | `requirements/institutional-learning/tests/institutional-learning.test.mjs::WHAT[INSTITUTIONAL-LEARNING-006] positive and negative experiences use the same non-punitive bounded enhancer` |
| INSTITUTIONAL-LEARNING-008 | `requirements/institutional-learning/tests/institutional-learning.test.mjs::WHAT[INSTITUTIONAL-LEARNING-008] occurrence replay keeps the first frozen result and does not create a second disposition` |
