# cognitive-environment — WHY

## 领域动力与核心张力

参与者在思考与行动时接触到的认知材料天然包含长期与瞬时两个层次：

```text
长期认知层 ──► World（通用常识）· Role（我是谁/Role Law）· Library（继承的技术书籍）
瞬时上下文 ──► Runtime（此刻生命周期事实）· Mission（当前委托的具体使命）
```

若将两者混淆，会导致严重退化：
- **瞬时状态污染长期自我**：若把任务进度、临时执行档位或具体工具清单写入 Role Law，换任务或换绑定就会导致人格与自我模型漂移。
- **知识越权授信（Knowledge ≠ Authority）**：技术书籍指导「如何识别缺陷与验证」，但拥有该知识绝不等于获得了修复代码或运行环境的权能。知识可跨越权能边界流动，但权能不随知识流动。
- **组合权威冲突**：若缺乏统一的组合协议，各层材料互相覆盖甚至产生全序优先级混淆，导致认知环境撕裂。

`cognitive-environment` 的核心不变量：
- **五层正交组合**：World、Role、Library、Runtime、Mission 各属单一主权威，按规范顺序组装。
- **工具清单不入 Role 章节**：System Prompt 聚焦自我模型与职责边界，不机械枚举瞬时工具列表。
- **Role Law 跨档恒定**：同一 Office 的 fast 与 deep 档共享相同的自我模型与 Role Law。
- **Pair Hint 作为核心 Craft 载体**：规范协作求助、持续维护就绪前沿（Ready Frontier）的无阻塞并发调度，并在复杂非线性任务出现时提醒参与者可用 `assume` 的持久 jq 画板外化、重排和反复编辑中间结构，而不是把聊天 token 流误当唯一工作内存。

## 破裂后果

- 参与者通过阅读技术书籍误认为自身拥有对应领域的执行权能。
- 阶段性任务或瞬时生命周期事件修改长期自我模型，导致换执行者时人格漂移。
- 提示词内机械堆砌工具列表，破坏提示词前缀稳定性并分散模型注意力。

## 边界与关系

- `participant-identity`：提供 Role 与 Persona 身份事实；本包组织其面向模型的认知呈现。
- `office-capability`：定义职位的权能边界；本包负责引用而非重新定义这些权能。
- `attention-regulation`、`concern-routing` 与 `institutional-learning`：提供微原语动作；本包在 Pair Hint 中提供高显著性触发提醒。

## DEPENDS ON

- `participant-identity`
- `office-capability`
- `attention-regulation`
- `concern-routing`
- `institutional-learning`
