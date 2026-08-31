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

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| INSTITUTIONAL-LEARNING-001 | `requirements/institutional-learning/tests/institutional-learning.test.mjs::WHAT[INSTITUTIONAL-LEARNING-001] celebrate and regret accept one raw natural-language experience without a rule template` |
| INSTITUTIONAL-LEARNING-002 | `requirements/institutional-learning/tests/institutional-learning.test.mjs::WHAT[INSTITUTIONAL-LEARNING-002] one enhancer evaluation yields exactly one ABSORB BIRTH or DISCARD disposition with no score state` |
| INSTITUTIONAL-LEARNING-003 | `requirements/institutional-learning/tests/institutional-learning.test.mjs::WHAT[INSTITUTIONAL-LEARNING-003] enhancer is bounded to the supplied experience and live rulebook snapshot` |
| INSTITUTIONAL-LEARNING-004 | `requirements/institutional-learning/tests/institutional-learning.test.mjs::WHAT[INSTITUTIONAL-LEARNING-004] unsafe raw experience cannot bypass behavior-rule admission by directly birthing a rule` |
| INSTITUTIONAL-LEARNING-005 | `requirements/institutional-learning/tests/institutional-learning.test.mjs::WHAT[INSTITUTIONAL-LEARNING-005] no reusable trigger or nonduplicate mechanism degrades to DISCARD rather than attention-tax debt` |
| INSTITUTIONAL-LEARNING-006 | `requirements/institutional-learning/tests/institutional-learning.test.mjs::WHAT[INSTITUTIONAL-LEARNING-006] positive and negative experiences use the same non-punitive bounded enhancer` |
| INSTITUTIONAL-LEARNING-007 | `requirements/institutional-learning/tests/institutional-learning.test.mjs::WHAT[INSTITUTIONAL-LEARNING-007] celebrate alone resurfaces deferred work and the same durable fact updates attention coverage` |
| INSTITUTIONAL-LEARNING-008 | `requirements/institutional-learning/tests/institutional-learning.test.mjs::WHAT[INSTITUTIONAL-LEARNING-008] occurrence replay keeps the first frozen result and does not create a second disposition` |
