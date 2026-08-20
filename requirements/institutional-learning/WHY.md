# institutional-learning — WHY

## 领域动力与核心张力

若运行中的经验仅停留在一次性会话的临时记忆中，系统无法获得跨生命周期的组织级进化；但若每次经历都机械新增一条规则，规则库又将迅速膨胀为沉重的组织瘢痕（institutional scar tissue），带来灾难性的注意力税（attention tax）。

`institutional-learning` 的存在理由是建立经验向制度进化的受控收敛通道：
1. **显式言语行为**：通过 `celebrate(experience)` 将偶然成功提炼为可复制的能力，通过 `regret(experience)` 将教训转化为组织免疫机制；
2. **三元收敛裁决**：经验经由私有 Enhancer 提炼为比单次经历更一般且未来可识别的机制，并严格在 `ABSORB`（已被现有规则表达）、`BIRTH`（产生全新规则并提交准入校验）与 `DISCARD`（偶然/局部/不值得长期注意力税）中三选一，保证经验能够演进制度而不使规则集无界增长；
3. **注意力闭合（Attention Closure）**：`celebrate` 在完成经验学习后，于尾部统一弹出当前参与者之前通过 `defer` 暂缓的旁支工作，使注意力从主线任务优雅过渡，而不将旁支提前演化为阻塞性债务。

## 核心不变量

1. **原始经验输入**：`celebrate` 与 `regret` 接受自然语言经验输入，不强求调用者预先结构化为规则格式或猜测内部标识。
2. **单次收敛性与有界评估**：每次学习事件最终有且仅有一个提交的 disposition；Enhancer 单次调用评估，不引入递归增强或中间未决状态。
3. **纯粹机制抽象**：Enhancer 仅以经验与当前 live Rulebook 为输入，不扩大学习范围至外部网络或无关仓库调查，不把局部瞬态事实升格为永久规则。
4. **准入单入口与零副作用**：仅 BIRTH 允许向 `behavior-diagnosis` 提交新规则 candidate；ABSORB 与 DISCARD 产生零规则变更。规则持久化与命名空间统一由 `behavior-diagnosis` 裁决。
5. **原子提交事务**：学习结论、规则准入与暂缓工作弹出必须在单笔原子持久化事务中提交，版本冲突时零提交并安全重试。

## 边界与失效模式

- **不负责规则诊断与执行**：既有规则如何被检测与诊断归 `behavior-diagnosis`。
- **不负责处置手册交付**：规则如何向 Main 呈现归 `guidance-delivery`。
- **不负责暂缓工作队列管理**：`defer` 队列与弹出语义归 `attention-regulation`。

**失效表现（RED）**：
- 每次学习调用均机械新增规则，导致规则库只增不减；
- 失败被隐瞒，返回伪造的学习成功收据；
- 新规则绕过 `behavior-diagnosis` 的准入校验直接写入；
- `celebrate` 在完成经验收敛前即提前执行或清空暂缓工作。

## DEPENDS ON

`institutional-learning → attention-regulation, behavior-diagnosis, durable-events`
