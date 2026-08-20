# attention-regulation — WHY

## 核心动机与不可替代性

LLM 在长会话中极易产生三种注意力和范围退化：
1. 把已经足够的判断重新打开、陷入无休止的证据搜寻；
2. 把自己已经决定放弃的推论、方向或非正式假设继续背在身上；
3. 因害怕遗忘非阻塞旁支而当场扩张当前任务范围。

本包为 participant 提供三个原子退出动作（speech act），分别用于终止无效深入、解除认知自我承诺、延后非阻塞工作：

- `enough(decision)`：当前信息已足够支撑行动；停止重开同一判断。
- `abandon(commitment)`：显式放弃自我承诺或推论方向，卸下心理负担。
- `defer(new_work)`：登记新发现但非阻塞的工作，移出当前工作记忆，待后续统一露出。

`enough` 与 `abandon` 不维护持久状态机，不产生权威转移，价值在于通过工具调用边界钉住认知转折。`defer` 则提供最小的跨工作记忆暂存队列，并在后续 celebration 尾部重新浮现。

## 失败模式（RED）

- **无效重开**：在无新决策证据时，持续重开已判明结论。
- **僵尸承诺**：已明确放弃的推论因前文记忆重量反复复活。
- **范围漂移**：因担忧遗忘将非阻塞旁支伪装成当前 mission 义务。
- **状态失真**：`defer` 工作在 celebration 时丢失、重复激活，或被系统自动提升为硬性欠账。

## 边界与被拒方案

- **不并入 `cognitive-environment`**：后者管理长期角色模型与全局规范；本包仅负责单次交互中的注意力生命周期。
- **不并入 `obligation-ledger`**：`defer` 记录的是延后工作而非当前 mission 欠账，禁止将其自动转化为 formal debt。
- **不并入 `epistemic-reasoning`**：`enough` 是参与者主动声明证据负担已满足的动作，不替代模型推理置信度。
- **拒绝通用状态机与规划器**：不引入 priority、deadline、background worker 等复杂编排，仅保留最小原子动作。

## DEPENDS ON

- `participant-identity`
- `durable-events`
