# action-affordance — WHY

## 核心动机与不可替代性

在参与者（participant）做决定的瞬间，唯一可靠的认知输入是当前决策界面（decision surface）所呈现的动作契约。被调用方的 Role Law 并不会直接展现在调用方眼前；仅仅掌握长期的世界模型，并不等于清楚当前具体动词（verb）的精确后果与边界。

动作契约必须在调用瞬间完整回答核心五问：
1. **What act happens?**（发生什么动作）
2. **When does this act fit?**（何时适用）
3. **What tempting nearby act does this NOT perform?**（不执行哪些临近的诱惑行为）
4. **What does a successful return establish?**（成功返回确立了什么事实）
5. **What does each non-obvious argument mean?**（非显然参数的具体语义）

本包独立负责调用边界上的局部认知合同。它保证所有动作描述与参数语义可以在不改动长期认知与 Office 资格模型的前提下独立演进，同时确保边界在调用界面上得到完整镜像。

## 失败模式（RED）

- **正向单向描述**：动作描述只写目标（如“建立仓库事实”），未标明因果只读或禁止改码的负边界，导致调用方越界布置任务（例如要求 Inspector 修代码）。
- **语义降维误解**：将“mechanical”误解为“代码物理改动小”而非“产品含义已被决定”，导致错误使用行为修复工具进行产品设计决策。
- **一词多义与同名异义**：同一工具名称在不同场景下承载不同的语义契约，破坏契约一致性。
- **权限退化为裸枚举**：将代表能力与责任差异的选择项（如 calling）退化为无语义的裸枚举。
- **边界镜像缺失**：因被调用方的 Role Law 已有约束，就从调用方的工具描述中删除对应边界，导致调用方在决策面无法感知限制。

## 边界分离

- **与 `office-capability` 分离**：后者规定 Office 产生后果的权威资格；本包规定调用瞬间 decision surface 上呈现的动作边界与后果。
- **与 `capability-enforcement` 分离**：后者保证 Provider 可见接口与 Runtime 执行能力同源；本包负责动作语义与描述契约。

## DEPENDS ON

- `office-capability`
- `participant-horizon`
