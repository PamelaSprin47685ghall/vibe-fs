# review-assurance — WHY

## 1. 存在理由与核心矛盾

`review-judgement` 确立了 `PERFECT` 与 `REVISE` 的判定哲学，但 Reviewer 输出裁决并不等同于系统获准消费该裁决。`review-assurance` 必须解决关键的因果证明问题：

> 当前判断是否针对当前被审对象？它是否真正消费了必要的 challenge？其证据链是否完备到足以供下游系统消费？

若缺乏严格的保证机制，系统将退化为以下失败形态：

1. **针对过期代码状态的确认被错误消费**：代码发生 rebase 或后续提交后，旧的确认仍被视作当前状态的证明，导致系统消费了针对已失效对象的判断。
2. **凭同名请求或消息猜测因果**：仅凭 AuthorityRoot 或 PhysicalMessageId 相同就假定第二次 PERFECT 成立，无法证明模型确实消化了 challenge。
3. **依赖外围可变状态补充身份**：Guard 依赖外部 Map 或存储的布尔标志推导确认状态，导致并发或崩溃恢复时发生身份串扰与空确认。
4. **未就绪的判断被提前消费**：仅有 `VerdictKnown` 而无完整报告时即放行下游操作，导致下游拿到空壳报告。
5. **系统故障被伪装为业务结论**：Reviewer 会话创建、分配或报告物化失败被错误记录为业务 REVISE，迫使执行者去修复系统本身的故障。
6. **过程判断混淆终审证明**：过程评审单次 PERFECT 被计入终审的 dual-PERFECT 证据链，稀释了终结质量门的证明强度。

`review-assurance` 保证：**Judgement 的消费资格必须基于有界事实记录（request-range bounded LWR）、新鲜证人（当前 tree/barrier）以及因果确认（challenge 在物理执行边中被真实消费）建立**。

## 2. 独立存在测试（Independent Change Test）

若重写 dual-PERFECT 的因果编排、witness 数据结构或 record-ready 代数，`review-judgement` 的判断哲学（如 discrimination 准则、材料性判定）无需任何改动。

反之，若整体替换 Reviewer 的提示词引导或打分维度，只要其输出仍为强类型 verdict，`review-assurance` 的因果确认链与失效判定同样保持不变。两个包拥有正交的失败域。

## 3. 核心不变量与失败判定

系统在以下任一情况发生时判定为 RED：

- 系统消费了针对旧 Git tree、错误 frontier、未消费 challenge 或缺失报告的 judgement。
- 确认判定依赖 AuthorityRoot 或文本猜测，而非基于强类型物理执行标识建立因果。
- 确认依据依赖外围 Map 或存储的布尔标志，而非自包含 witness 的派生谓词。
- 在报告未达成 record-ready 时提前持久化 `TodoReviewConcluded`。
- record-ready 判定使用较晚的 XTrace head、分步读取不一致 snapshot，或采用 wall-clock 轮询。
- 基础设施异常（如物化失败、传输超时）被折叠为业务 PERFECT 或 REVISE。
- 过程评审 verdict 计入终审 dual-PERFECT 代数，或过程 REVISE 被视作终审拒绝事实。

## 4. 依赖边界

```text
DEPENDS ON: review-judgement, semantic-trace, durable-events, causal-wait
```
