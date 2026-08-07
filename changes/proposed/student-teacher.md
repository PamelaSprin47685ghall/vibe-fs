# Student & Teacher

未裁决候选。不是当前规范，不得直接据此修改生产代码。

## Current baseline

当前公开 Agent、内部 Agent、Prompt Authority、工具权限、Session、持久化和 Skill 文件写入
由现有正式条款与实现承担。系统没有 Student/Teacher 角色、学习/编译请求种类、持续 Teacher
Session 或临时 `QA.md` 协议。

本 Proposal 只描述新增的显式学习流程，不把现有 Agent、Prompt、Journal 或 Skill 规范复制为基线。

## Proposed delta

用户显式选择 Student 后，系统进入一个先学习、后编译的流程：Student 持续向同一个内部
Teacher 提问，完整自然语言轨迹落入任务私有 `QA.md`，最后由同一个 Student 把轨迹编译成
一个或多个 SKILL。

### Trigger and roles

- 唯一触发条件是用户明确选择 Student；不做意图识别或自动路由，默认关闭。
- Student 是用户可见的公开 Agent；Teacher 是任务私有、模型列表不可见的叶子内部 Session。
- 每个 Student run 只绑定一个 Teacher。可证明原 Session 存在时复用；可证明永久丢失时才创建
  Replacement 并用完整 QA 恢复；无法证明时 fail closed。
- Teacher 不直接向用户输出，Student 不直接调查外部世界。

### Request profiles and tools

- 学习阶段的 Student provider-visible 工具只有 `teacher`。
- Teacher 使用其裁决后的调查工具集，并必须用专用 `return` 返回自由文本。
- 编译阶段使用独立、原子冻结的 request profile，只开放读取 QA、写入目标 SKILL 和最终
  `return` 所需能力。
- schema 可见面与 runtime execution gate 必须同时验证 profile；请求种类不从文本推断。

### QA ownership

- 用户原始请求是 QA 的第一段；之后每个 Student 问题、Teacher 回答和最终综合按发生顺序追加。
- 每个自然语言输入先 durable 写入 QA，再触发对应外部效果。
- QA 是该学习任务唯一知识状态；不建立知识图谱、coverage 表、结构化 TeacherAnswer 或旁路 Journal。
- 框架不解析 QA 中的事实、决策、置信度、分支或收敛语义；文本不添加角色标签、字段或固定问答模板。
- 写入必须原子且重入幂等；中间损坏 fail closed，不猜测修复。

### Control flow

1. Student 提出一个当前最有价值的问题，并给出自己的最佳猜测。
2. 框架先追加问题，再调用同一 Teacher。
3. Teacher 调查真实环境；未 `return` 就 idle 时，只 nudge 同一 Teacher。
4. Teacher return 先追加回答，再交给 Student。
5. Student 自行判断是否还有高价值未知；学习阶段 idle 表示进入编译，不由框架判定知识收敛。
6. 编译阶段 Student 重读完整 QA，生成一个或多个自然边界的 SKILL；未最终 return 就 idle 时继续 nudge。
7. 最终 return 时，框架先删除 QA 并确认不存在，再提交 terminal 和用户可见完成说明。

第一版不支持从编译阶段返回学习阶段。用户取消、写入失败、Teacher 失败和删除失败都必须
保留可恢复事实并 fail closed；不得静默创建新 Teacher 或宣称完成。

### Knowledge semantics

Student/Teacher 之间只交换自由自然语言。第一性原理压缩与语义无损是编译 Prompt 的目标，
不是框架可机械证明的字段映射。机器只证明顺序、持久化、工具边界、Session 连续性、清理和文件形态。

## Impact map

- what：Agent 触发/可见性、请求 profile、idle/return、QA 生命周期、取消与失败行为。
- shape：Student run、Teacher link、QA writer、tool gates、compile writer 和 terminal writer。
- how：持续问答、nudge、恢复、编译、删除后终止算法。
- proof：Agent/tool/session/QA/idle/return/可见性 canaries 与重放测试。
- code/resources：Agent 配置、Prompt assets、Session/Prompt/Journal/File adapters、Skill 输出。

## Alternatives

1. 普通 Agent 直接写文档：不能确保写作前完成知识获取。
2. 每轮创建新 Teacher：丢失连续上下文，并增加恢复歧义。
3. 多个 Teacher：引入合并、冲突和归因协议，首版收益不足。
4. 结构化 TeacherAnswer/知识图谱/coverage：预设未知知识的形状，限制发现。
5. 自动触发：可能在用户未授权时产生长流程和临时敏感材料。
6. 用 Journal 复制 QA 内容：形成双写知识源。

## Migration / cutover

1. 先裁决公开 Student 与内部 Teacher 的角色、tier、工具面和 Prompt Authority 影响。
2. 用 Host canary 证明同一 Teacher Session 可继续、内部身份不可见、idle/nudge/return 可区分。
3. 建立任务私有 QA 的路径、安全、原子追加、恢复和删除协议。
4. 接入学习 profile，再接入编译 profile；每阶段先证明 provider schema 与 execution gate 一致。
5. 最后接入 SKILL 写入和删除后 terminal；默认关闭，完成 canary 后再显式启用。

## Compatibility disposition

Compatible when disabled。启用前必须对新增 Agent 配置、临时文件位置、Skill 写入权限和任何
持久化 schema 选择作出 ExplicitMigration 或 ExplicitReset 裁决。

## Proof plan

- 触发：未选择 Student 时零副作用；明确选择才创建 run。
- 工具：学习/Teacher/编译三个 profile 的 schema 与 runtime gate 均 fail closed。
- Session：后续问题复用同一 Teacher；replacement 只在永久丢失证据成立时发生。
- QA：原始请求第一、追加顺序稳定、先落盘后效果、重复恢复幂等、中间损坏拒绝。
- idle/return：Teacher idle nudge 同一 Session；Student 学习 idle 进入编译；编译 idle 不误完成。
- terminal：删除失败不提交完成；删除成功且确认不存在后才 terminal。
- 可见性与安全：Teacher 不进入可见枚举；QA 不进入日志或错误回显；取消后可恢复或安全清理。

## Decision owner

Wanxiangshu 项目 Owner。

## Admission blockers

- 需要裁决 Student/Teacher 的正式角色、tier 和精确工具集合。
- 需要确定 QA 的仓库外私有位置、敏感数据保护和崩溃恢复策略。
- 需要 Host 证据证明 Session 继续、idle/nudge、内部不可见性和两种 request profile 可可靠实现。
- 需要确认 Skill 写入目标、冲突处理和最终删除的兼容性策略。
