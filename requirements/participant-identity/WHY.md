# participant-identity — WHY

## 领域动力与核心张力

系统内关于「参与者身份」存在三条完全正交的维度：

```text
Role              = Office 职位：承担哪类系统职责（Manager / Orchestrator / Coder / Inspector / ...）
Persona           = 自我模型：如何自称与理解自身（Engineer / Lead / ...）
ExecutionBinding  = 物理执行者：当前使用的模型、推理档位与租约配额（fast-coder / deep-coder）
```

当三者混淆时，会产生严重的领域退化：
- **换执行者变成换人**：模型在执行阶段发生 Peer Fallback 或升档时，责任边界与自我模型发生漂移。
- **机器身份污染自我认知**：底层的机器执行档位（`fast-*` / `deep-*`）渗透到面向模型的提示词中，使其自称机器名。
- **副本漂移出独立自我**：用于投机调查的执行副本（Replica）若独立绑定身份，会演化出脱离主体的虚假人格。

`participant-identity` 的核心不变量在于：**换执行者不等于换人**。在单个 participant life 内，Role 与 Persona 绝对稳定且不可变，仅允许 ExecutionBinding 在受控机制下切换。

## 破裂后果

- 模型或执行上下文切换能够静默改写责任模型或 Persona 自称。
- 同一参与者的不同执行上下文被误判为多个独立主体。
- 内部调度拓扑与机器档位泄漏至认知层，破坏提示词稳定性与权限边界。

## 边界与关系

- `office-capability`：定义职位的权能后果；本包定义行动者身份事实。
- `capability-enforcement`：保证可见能力与可执行能力同源；本包提供身份输入。
- `session-ontology`：定义 session 的执行分类与归属；本包消费其主体归属事实。

## DEPENDS ON

- `session-ontology`
