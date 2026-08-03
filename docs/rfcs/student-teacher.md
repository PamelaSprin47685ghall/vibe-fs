# RFC/student-teacher — Student & Teacher：主动学习与 SKILL 编译

Status: proposed

> 合入说明（2026-08-02）：本文档由 `PENDING/16-Student-Teacher方案.md` 原样合入（正文未改）。
> 原稿「状态：APPROVED AS FINAL ARCHITECTURE」随合入更新为「已合入规范」；实现仍受
> Host canary（第二十三节 LEARN-082…088 清单）门禁。本文档为 Student/Teacher/QA/SKILL
> 语义的规范主体。

---


状态：未来设计（已批准但未启用）
实现门禁：IMPLEMENT ONLY AFTER HOST CANARIES —— 实现只能在 Host canary（第二十三节清单）通过后启动
文件：`docs/rfcs/student-teacher.md`
条款前缀：`LEARN-`

---

# 修订说明（RC1 → Final）

本稿经一次精确修订后批准为最终架构。架构先定稿、实现仍受 Host canary 门禁，因此不必等代码完成。本次修订的四条核心裁决：

1. 每个自然语言输入先进入 QA，再产生外部效果。
2. 用户目标、全部追问、全部回答、最终综合都必须存在于 QA。
3. StudentLearn / StudentCompile 的工具面必须进入原子 AttemptExecutionProfile（PROMPT-008）。
4. 删除所有未经证明的实现细节与文档展示噪音。

同时：删除“每轮成对提交”与结构化痕迹（分隔线、角色标签）；确立 Teacher 为叶子内部 Teacher Satellite 的最终裁决（LEARN-027）；区分 Teacher 复用 / Replacement / fail closed 三路恢复（LEARN-030）；收紧最终 return 的“先删除、后完成”顺序（LEARN-024）。实施细节（精确路径、错误名、Agent 命名、canary 数量、写入机制、tier 映射、编译工具组合）降为实施建议。

目标版本：首个生产版本
默认状态：Student Agent 可选；用户未选择 Student 时完全不触发
核心产物：`.agent/skills/.../SKILL.md`

---

# 一、执行摘要

Student & Teacher 用于解决一个长期存在的问题：

> Agent 在尚未真正理解问题时就开始写文档，最终产出形式完整、知识稀薄、遗漏关键决策的文本。

本功能不尝试通过更长 Prompt 要求普通 Agent“认真一点”。它改变知识生产流程：

```text
用户主动选择 Student
→ 用户原始请求先原样追加进 QA.md
→ Student 持续向同一个 Teacher 学习
→ 每个自然语言输入先进入 QA.md，再产生外部效果
→ Teacher 使用完整工具调查真实环境
→ Student 判断已无高价值问题后主动 idle
→ 框架把 QA.md 路径交给同一个 Student
→ Student 将完整问答语义无损地编译为一个或多个 SKILL
→ Student 调用 return
→ 框架先删除 QA.md 并确认不存在，再结束对话
```

最终裁决：

1. Student 是用户主动选择的公开 Agent。
2. 用户选择 Student 即视为明确触发，不做意图识别、不做自动路由。
3. Student 在学习阶段只有一个工具：`teacher`。
4. 每个 Student 工作任务绑定同一个持续 Teacher Session。
5. Teacher 每轮都继续该 Session，不创建失忆式临时 Teacher。
6. Teacher 拥有完整工具能力，并通过专用 `return` 把自由文本返回 Student。
7. Student 与 Teacher 之间不使用任何知识 schema。
8. 问题、回答、结论、分支、置信度、证据均不结构化。
9. 工具参数只有自然语言正文。
10. `QA.md` 是本次学习任务唯一权威状态；用户原始请求、全部追问、全部回答与 Student 最终综合都必须存在于其中。
11. `QA.md` 保存按真实发生顺序追加的完整自然语言思想过程，不维护旁路 Journal、知识图谱或决策表。
12. 框架不理解 QA 内容，不判断什么是事实、决策、错误、分支或收敛。
13. Teacher 未调用 `return` 就 idle 时，框架 nudge 同一 Teacher。
14. Student 学习阶段 idle 时，不视为失败；它表示 Student 决定进入 SKILL 编译。
15. 编译阶段 Student 获得读取 QA、写入 SKILL 与最终 `return` 所需工具。
16. Student 编译提示词同时要求第一性原理表达与语义无损。
17. 两者冲突不靠字段映射、coverage 表或机器验证解决，只靠精心设计的自然语言提示词解决。
18. Student 最终调用 `return` 后，框架先删除 QA.md 并确认不存在，再提交终止并显示完成说明。
19. Student 编译阶段未调用 `return` 就 idle 时，框架持续 nudge。
20. 除 Student Agent 外，现有 Agent 行为完全不变。

本功能的本质不是“双 Agent 写作”，而是：

> 用持续追问完成知识收敛，再把完整思想历史压缩为可复用技能。

---

# 二、问题定义

## LEARN-001：普通文档 Agent 的失败模式

普通 Agent 收到以下请求：

```text
为了实现万象术，需要宿主的哪些能力？
```

或：

```text
阅读本项目库，看看能学到什么编程经验。
```

往往立即执行：

```text
读取少量文件
→ 套用常见分类
→ 输出一篇完整文章
```

问题不在文字质量，而在开始写作前没有完成知识获取。

模型通常不知道：

* 用户术语真正指什么；
* 哪些架构决策是核心，哪些只是实现偶然；
* 仓库文档和源码是否一致；
* 哪些经验可以迁移；
* 哪些反例会推翻当前概括；
* 哪个问题的答案会改变后续所有问题；
* 应生成一个还是多个 SKILL。

在未知未消除前开始写作，本质是用先验补齐事实。

## LEARN-002：Student 的职责

Student 不负责直接调查外部世界。

Student 负责：

```text
识别自己不知道什么
→ 提出最有价值的一个问题
→ 给出当前最佳猜测
→ 接受 Teacher 的纠正或重构
→ 更新理解
→ 继续提出下一问
→ 判断何时已没有值得获取的高价值信息
→ 编译最终 SKILL
```

Student 是主动学习器，不是普通作者。

## LEARN-003：Teacher 的职责

Teacher 负责：

```text
理解 Student 当前问题
→ 必要时拒绝问题前提
→ 使用工具调查真实环境
→ 自由组织答案
→ 只补充、纠正或重组有价值的信息
→ 通过 return 把完整自然语言答复交还 Student
```

Teacher 不是表单填写器，也不是一次性子任务 Worker。

## LEARN-004：QA.md 的职责

`QA.md` 负责保存：

```text
Student 提出的原始问题
Teacher 返回的原始回答
后续 Student 对前文的重新理解
Teacher 对旧理解的纠正
所有探索弯路、反例、例外与最终共识
```

`QA.md` 不是摘要，不是派生视图，不是缓存。

它是本次学习任务的唯一权威状态。

---

# 三、设计第一原理

## LEARN-005：结构化控制会限制未知发现

结构化返回类型隐含一个危险前提：

> 系统已经知道答案由哪些类别组成。

例如：

```fsharp
type TeacherAnswer =
    { Decisions: Decision list
      Assumptions: Assumption list
      Evidence: Evidence list
      OpenQuestions: Question list }
```

这种类型会迫使 Teacher 把新发现放进预先存在的抽屉。

但探索任务真正有价值的回答可能是：

* Student 的整个分类方式都错了；
* 两个看似无关模块其实在守同一个不变量；
* 真正值得学习的是一个失败实现；
* 源码与文档的矛盾比任何成功设计都重要；
* 问题不是“需要什么能力”，而是“哪些责任不应属于宿主”；
* 当前没有合适名称，需要先发明概念。

因此本功能禁止知识 schema。

## LEARN-006：特殊疑问优于一般疑问

一般疑问通常把答案限制为既有命题的确认：

```text
是否需要事件日志？
是否需要跨进程恢复？
是否应该双层检查权限？
```

特殊疑问允许 Teacher 改变问题空间：

```text
这个系统怎样保存事实，其中最危险的误解是什么？
缺少跨进程恢复会破坏哪条不变量？
工具能力从哪里真正进入系统，哪条路径最容易绕过权限？
```

Student Prompt 应优先鼓励：

```text
什么
为什么
怎样
哪里
在什么条件下
什么会推翻当前理解
哪个例外最重要
真正的问题是什么
```

只有验证已经精确形成的命题时，才使用是／否问题。

## LEARN-007：一次只问一个问题

Student 每轮只能向 Teacher 提交一个中心问题。

允许在同一段自然语言中包含：

* 当前理解；
* 当前最佳猜测；
* 猜测依据；
* 为什么该问题重要；
* 请求 Teacher 不要重复已正确部分；
* 请求 Teacher 推翻错误前提。

但不得同时塞入多个彼此独立的问题。

理由：

```text
Teacher 的当前回答
可能改变后续所有问题
```

一次提交十个问题等于提前假定问题树不会变化。

## LEARN-008：结构化控制流，零语义结构

框架只控制：

```text
谁拥有控制权
调用哪个工具
何时转交消息
何时观察到 idle
何时进入编译
何时删除临时文件
何时终止
```

框架不理解：

```text
当前问题是什么
Teacher 是否答对
哪些内容是事实
双方是否达成共识
还有多少分支
是否存在矛盾
是否应该结束学习
最终应生成几个 SKILL
```

即：

> 结构化控制流；零语义结构。

## LEARN-009：第一性原理与信息无损

最终编译同时要求：

```text
第一性原理表达
+
不丢失任何有价值信息
```

这里的第一性原理不是“只留几句抽象口号”。

它表示：

> 找到能够解释并生成全部具体结论的最小充分知识。

“不丢失任何信息”也不是逐字复制全部聊天。

它表示：

> 每个会改变理解、判断或执行结果的语义区别，都必须在最终 SKILL 中被直接表达、由更基础原则完整推出，或明确保留为未解决的不确定性。

允许删除：

* 寒暄；
* 纯重复；
* 真正同义的措辞；
* 已被后文完全替代且无警示价值的中间表达。

不得删除：

* 适用条件；
* 边界；
* 例外；
* 反例；
* 失败模式；
* 决策理由；
* 重要实例；
* 被纠正但具有警示价值的错误；
* 未解决的矛盾；
* 只有特定宿主条件下才成立的结论。

该冲突只由最终编译 Prompt 化解。

禁止引入机器 coverage 表、Fact ID、字段映射或知识图谱来解决。

---

# 四、目标与非目标

## LEARN-010：目标

本功能必须做到：

1. 让用户显式进入深度学习模式。
2. 允许用户提交高度模糊的探索目标。
3. 让 Student 通过连续追问逐步发现问题空间。
4. 让 Teacher 使用真实工具调查源码、文档、运行环境和外部依赖。
5. 保持同一个 Teacher Session 的连续上下文。
6. 完整保存所有自然语言问答。
7. 不让框架预设知识本体。
8. 不要求 Teacher 按固定格式回答。
9. 不要求 Student 维护结构化理解状态。
10. 在学习结束后生成一个或多个边界自然的 SKILL。
11. 让未参与对话的 Agent 能直接使用这些 SKILL。
12. 编译后清理临时 QA 文件。
13. 不影响普通 Agent 的执行路径。

## LEARN-011：非目标

本功能不负责：

1. 自动判断普通用户请求是否适合学习模式。
2. 自动把普通 Agent 请求改写为 Student 请求。
3. 为 Teacher 的答案打置信度分。
4. 自动识别事实、观点、推测和决策。
5. 自动建立知识图谱。
6. 自动计算信息增益。
7. 自动证明双方已经达成共识。
8. 自动验证最终 SKILL 是否语义无损。
9. 强制限定提问轮数。
10. 用结构化返回值替代自然语言。
11. 把 QA.md 转换为 Journal 事件流。
12. 让其他 Agent 隐式调用 Student。
13. 在用户未选择 Student 时产生任何额外模型调用。

---

# 五、用户触发

## LEARN-012：唯一触发条件

```text
用户显式选择 Student Agent
```

即认为用户主动要求：

```text
不要立即完成任务
先通过 Teacher 深入学习
最后把知识整理为 SKILL
```

不再检查：

* 请求是否足够模糊；
* 是否包含“学习”“研究”“文档”等关键词；
* 是否值得生成 SKILL；
* 是否已有明确答案；
* 预计需要多少轮；
* 成本是否划算。

用户选择 Student 就是授权。

## LEARN-013：禁止隐式触发

以下行为禁止：

```text
普通 Coder 觉得问题复杂 → 自动转 Student
Manager 觉得需要调查 → 自动创建 Student
系统检测用户请求模糊 → 自动重写为 skill-grill
普通 Agent 写文档前 → 自动启动 Teacher
```

若用户未选择 Student，现有行为保持不变。

## LEARN-014：模糊请求合法

Student 必须接受：

```text
阅读本项目库，看看能学到什么编程经验。
```

```text
研究一下这个系统，有什么值得沉淀的。
```

```text
看看万象术应该怎么实现。
```

```text
把这里真正重要的知识学明白。
```

用户无需预先提供：

* 问题树；
* 输出分类；
* SKILL 数量；
* 关注模块；
* 评估标准；
* 明确终止条件。

发现这些内容本身就是 Student 的工作。

---

# 六、Agent 设计

## LEARN-015：新增角色

新增两个 CanonicalRole：

```fsharp
type Role =
    | ...
    | Student
    | Teacher
```

Student 是公开角色。

Teacher 是内部角色。

## LEARN-016：Agent ID

规范要求：

* 公开 Agent 恰有两个 Student 变体（fast/deep 各一）；
* 内部 Agent 恰有两个 Teacher 变体（fast/deep 各一）；
* 与现有 fast/deep tier 体系一致。

具体 Agent ID 是实现选择，例如：

```text
fast-student
deep-student
fast-teacher
deep-teacher
```

公开 Agent catalog 只展示两个 Student 变体。

以下位置不得暴露 Teacher：

```text
用户 Agent 选择器
公开 Authority Root agent enum
fork-agent enum
fork-manager enum
list 的 Agent catalog
任何普通 Agent 可见工具描述
```

Teacher 只由 Student 的 `teacher` 工具创建或恢复。

## LEARN-017：Tier 映射

默认建议：Student 与 Teacher 同 tier（FastStudent → FastTeacher，DeepStudent → DeepTeacher）。

该映射是实现建议，不是规范；实现可因模型可用性调整，但必须满足：

* Teacher 绑定由实现固定，不得由 Student 在自然语言中选择 Teacher model；
* 发送 Prompt 仍遵循：

```text
Agent = Some effectiveAgent
Model = None
```

Host 通过 Agent 配置解析模型，避免绕过现有 Agent 身份与 AttemptExecutionProfile（PROMPT-008）纪律。

## LEARN-018：Student 工具能力

StudentLearn 请求（LEARN-050）的 provider-visible 工具集严格为：

```text
teacher
```

Student 不可见：

```text
read
glob
grep
write
edit
apply_patch
executor
fork-agent
fork-manager
join
list
fork-pty
verdict
浏览器
网络工具
最终 return
```

这是本功能成立的关键：

> Student 不能绕过 Teacher 自己调查，也不能在理解尚未收敛前直接写 SKILL。

## LEARN-019：Teacher 工具能力

Teacher 拥有当前系统允许提供给普通执行 Agent 的完整工具集合，并额外拥有：

```text
return
```

Teacher 可以：

* 读取仓库；
* 搜索源码；
* 检查配置；
* 运行测试；
* 使用终端；
* 调查 Host 行为；
* 查看外部资料；
* 委派现有工具能力；
* 在问题确实要求时执行必要验证。

Teacher 不因“教学角色”被结构性降为只读。

但 Teacher 仍服从系统全局安全规则：

* 破坏性操作需要现有授权；
* 不得绕过工具权限；
* 不得伪造执行结果；
* 不得把敏感信息泄露到不应出现的边界；
* 不得绕过 PromptDispatcher、Host 或现有执行合同。

“工具不限”表示本功能不额外收窄 Teacher，不表示取消系统全局安全边界。

## LEARN-020：Teacher 不直接对用户输出

Teacher 的普通正文永不成为用户可见最终回答。

Teacher 每轮只能通过专用 `return` 把文本返回 Student。

Teacher text-out、普通 idle 或会话正文都不能被框架当成有效回答。

---

# 七、工具合同

## LEARN-021：Student 的 teacher 工具

工具概念形态：

```typescript
teacher(message: string): string
```

只有一个语义参数：

```text
message
```

禁止增加：

```text
question
guess
context
branch
confidence
evidence
expected_format
remaining_unknowns
status
```

Student 可以在 `message` 中自由写入任何自然语言。

示例：

```text
我目前认为该项目最独特的部分不是 Agent 编排，
而是把 Provider 请求身份、Authority Root 与持久化事实绑定在一起。

如果这个理解正确，请不要复述。
请调查真正负责该约束的源码与 SSOT，
说明最容易被实现者误解的因果边界是什么。
如果我的分类本身错误，请直接推翻它。
```

## LEARN-022：teacher 工具执行语义

每个自然语言输入先进入 QA，再产生外部效果。一次 `teacher(message)`：

```text
把 message 原样追加至 QA.md
→ 确认落盘成功
→ 取得本 Student 任务绑定的 Teacher Session（不存在则创建）
→ 把 message 作为 Teacher 的下一自然语言输入
→ 等待该 Teacher 调用 return
→ 取得 return 的完整文本
→ 把 return 原样追加至 QA.md
→ 确认落盘成功
→ 把 Teacher 文本作为 teacher 工具结果交还 Student
```

即使 Teacher 创建失败、崩溃或永远没有回答，Student 的 message 也已经在落盘成功时成为 QA.md 中真实发生过的思想，不得回滚。

必须先持久化，再把内容交给下一方：

- message 未落盘 → 不发送给 Teacher；
- return 未落盘 → 不交给 Student。

否则接收方会看到一段不在权威状态中的知识。

## LEARN-023：Teacher 的 return 工具

工具概念形态：

```typescript
return(message: string): never
```

只有自由文本参数。

禁止增加：

```text
status
completed
blocked
exhausted
answer
corrections
decisions
next_question
```

Teacher 可以自由返回：

* 一段论证；
* 一个反例；
* 一组源码路径；
* 一段代码；
* 对 Student 前提的否定；
* 一个新的概念划分；
* 一个历史解释；
* 一个思想实验；
* 一个尚不能解决的疑问。

调用后，本 Teacher turn 结束，控制权返回等待中的 `teacher` 工具。

## LEARN-024：最终 Student return

最终编译阶段向 Student 提供：

```typescript
return(message: string): never
```

该 `return` 与 Teacher 内部 return 属于不同作用域。

Student 的最终 `return` 执行顺序是硬约束：

```text
接收 message
→ 删除本任务 QA.md
→ 确认 QA 已不存在（不存在视为删除成功）
→ 提交 terminal completion
→ 把 message 作为用户可见最终回复
```

禁止先向用户宣布完成再尝试删除。

删除失败时：不提交 terminal、不显示完成说明，return 以明确错误返回，Student 可重试 return（重试幂等，LEARN-068）。

最终 message 应说明生成了哪些 SKILL，不应重新复制全部知识。

## LEARN-025：工具名冲突

若 Host 不允许不同角色定义同名 `return`，内部实现可使用不同物理工具名：

```text
teacher_return
student_return
```

但 provider-visible 描述应保持简洁。

不得让模型理解内部路由细节。

---

# 八、Teacher Session

## LEARN-026：同一个 Teacher

每个 Student 学习任务恰好拥有一个活跃 Teacher Session：

```text
Student Run X
Teacher Session T_X
```

所有 `teacher(message)` 都发送到 `T_X`。

禁止：

```text
每轮创建新 Teacher
按问题分支创建多个 Teacher
Teacher text-out 后静默换新 Session
失败后创建另一个 Teacher 冒充原 Teacher
```

唯一例外：可证明原 Teacher 已永久丢失时创建 Replacement Teacher，且必须显式记录为灾难恢复替代（LEARN-030）。知识连续性由 QA.md 恢复；Session 连续性不能伪造。

持续 Session 保存：

* 已经解释过的概念；
* Student 曾经持有的错误理解；
* Teacher 使用工具发现的上下文；
* 尚未完成的推导；
* 已经访问的源码区域；
* 双方形成的语言习惯；
* 当前问题与旧问题的关系。

## LEARN-027：Teacher 是叶子内部 Teacher Satellite（最终裁决）

Teacher 的拓扑是确定裁决，不保留两条未来：

```text
Teacher = Student 的内部 Teacher Satellite
每个 Student WorkSession 恰好一个 Teacher
Teacher 是叶子
Teacher 无 Companion
```

叶子含义：

```text
自身不创建 Companion
自身不创建 Teacher
自身不进入普通 fork/join/list 资源
```

Session 关联由统一 `ManagedSessionKind`（HOST-008）决定，不得由当前角色、工具权限、当前 Run 或 Authority Root 临时推导。

本功能依赖统一 SatelliteRuntime；若尚未落地，先完成统一卫星结构迁移，禁止为 Teacher 单独复制一套 Session 所有权框架。

选择 Satellite 的理由：

* Teacher 只服务一个 Student；
* 不进入普通 fork/list/join；
* Teacher 已有持续 transcript，无需另起 Companion 总结 Teacher；
* 删除 Student 时可以级联 retire Teacher；
* 不为此功能复制独立 Session 所有权框架。

## LEARN-028：Teacher 创建

首次调用 `teacher` 时：

```text
Teacher 不存在
→ 创建对应 tier 的 Teacher Session
→ 绑定 Student Run
→ 安装 Teacher system prompt
→ 安装完整工具权限
→ 发送 Student message
```

创建必须 single-flight。

并发或重复触发不能产生两个 Teacher。

## LEARN-029：Teacher 后续调用

Teacher 完成本轮并 idle 后，下一次 `teacher(message)`：

```text
复用同一 Session
→ 发送下一自然语言 turn
→ 不清空历史
→ 不重新注入完整历史摘要
→ 不改变 Agent
→ 不改变 model
```

“continue Teacher session”指复用同一物理 Session 与 transcript。

它不要求所有问答属于同一个永不结束的 provider request。

## LEARN-030：Teacher Session 恢复

插件重启后，按可证明性分四种情况：

```text
正常路径：
  永远复用同一 Teacher Session。

可证明原 Host Session 仍存在：
  重新绑定并继续。

可证明原 Teacher 已永久丢失：
  创建 Replacement Teacher，
  将完整 QA.md 提供给它恢复，
  明确记录这是灾难恢复替代，而非同一 Session 继续。

无法证明：
  fail closed，不猜测，teacher 工具返回明确错误，不创建 Teacher。
```

知识连续性由 QA.md 恢复；Session 连续性不能伪造。

Replacement Teacher 必须：

* 在诊断日志中显式记录为 replacement，与正常继续可区分；
* 收到完整 QA.md 作为恢复输入；
* 不冒充原 Session，不声称是同一会话的延续。

旧 Teacher transcript 不是权威来源。

---

# 九、QA.md

## LEARN-031：唯一权威状态

Student–Teacher 功能不建立自己的 Journal。

不持久化：

```text
QuestionAsked
TeacherAnswered
DecisionConfirmed
BranchOpened
Converged
CompilationStarted
```

唯一持久状态是：

```text
QA.md 的完整内容
```

用户原始请求是 QA.md 的第一条内容，与问答同等权威（LEARN-071）。

若以下内容不一致：

```text
Student 当前上下文
Teacher 当前上下文
Host transcript
运行时内存
QA.md
```

知识判断以 QA.md 为准。

## LEARN-032：文件位置

具体路径是实现建议，不是规范：

```text
.agent/.tmp/student/<student-session-id>/<logical-run-id>/QA.md
```

以下要求是不变量：

1. 位于当前项目可读范围内。
2. Student 编译阶段的 `read` 可以访问。
3. Teacher 工具可以访问。
4. 路径不会进入正式 SKILL 搜索。
5. `.agent/.tmp/` 必须被版本控制忽略。
6. 文件权限限制为当前用户可读写。
7. 每个 Student Logical Run 使用独立目录。
8. 不复用旧任务 QA。

若项目规范已有插件临时目录，优先复用，不再创造第二套临时根。

## LEARN-033：内容完全非结构化

QA.md = 按真实发生顺序追加的自然语言字节流。

不使用 JSON、NDJSON、XML、front matter、YAML 或数据库字段。

框架不得添加：

```text
Round
Student
Teacher
Question
Answer
状态
角色标签
固定分隔模板
```

物理追加时只插入防止文本粘连所需的换行；框架不得写入分隔线、标题或任何标记。

换行不是协议：框架不解析、不依赖任何排版，不得根据内容恢复状态。

消息正文由模型自由书写；正文内的任何排版（包括分隔线、标题）都是模型文本的一部分，框架既不添加也不解析。

它仍然是连续自然语言，而不是协议格式。

## LEARN-034：逐字保存

追加时必须保留用户原始请求、Student message 与 Teacher return 的原始正文。

不得：

* 摘要；
* 改写；
* 翻译；
* 清理语气；
* 删除重复；
* 抽取“重要部分”；
* 只保存最终答案；
* 省略用户原始请求；
* 丢弃被推翻的旧理解；
* 用 Teacher transcript 替换 QA。

探索过程本身可能包含最终 SKILL 必须保留的反模式与决策理由。

## LEARN-035：先落盘，后生效

QA.md 没有“完整 Q&A 提交单位”。它是按真实发生顺序追加的自然语言字节流，每个输入在产生外部效果之前落盘：

```text
用户原始请求 → 追加 QA.md → 确认落盘 → 启动 Student
Student message → 追加 QA.md → 确认落盘 → 发送给 Teacher
Teacher return → 追加 QA.md → 确认落盘 → 作为工具结果交给 Student
```

Teacher 尚未 return 时，Student 的 message 仍然留在 QA.md——它是真实发生过的思想，不是需要 Pending 状态的半轮记录。

禁止成对提交：不得等到 Teacher return 后才把 Student message 一起写入，那会制造一段不在权威状态中的运行历史。

## LEARN-036：原子追加

逻辑语义是追加，物理要求是原子性：任何时刻 QA.md 要么是旧完整版本，要么是新完整版本，不得出现半段 UTF-8 或撕裂内容。

具体机制是实现选择，两种方案都合规：

```text
方案 A：读取旧 bytes → 拼接换行 + 正文 → 写 sibling temp → flush/fsync → atomic rename 覆盖
方案 B：持锁 append + fsync（锁覆盖写入与 fsync 全程）
```

禁止依赖裸的非原子写入。

QA 文件只存在于一个 Student 学习任务生命周期，体积受模型上下文与实际工作自然限制。第一版不为性能引入分块日志格式。

## LEARN-037：重复恢复

进程可能在以下窗口崩溃：

```text
Student message 落盘后、发送给 Teacher 前
Teacher return 落盘后、工具结果交付前
```

恢复时可能重试相同问答，导致同一正文被再次追加。

允许通过完整尾部字节比较避免明显重复。

若无法确定，宁可保留重复文本，不得删除可能存在的知识。

最终编译 Prompt 负责合并真正重复内容。

## LEARN-038：QA 不做 compaction

禁止：

```text
QA 太长 → 让模型摘要后覆盖
QA 太长 → 删除前半段
QA 太长 → 只保留结论
QA 太长 → 转为结构化数据库
```

QA.md 是权威，不能主动有损压缩。

若文件超出 Host 或模型实际可处理范围，这是功能容量边界，应显式失败或分批读取，不得静默删减。

---

# 十、Student 学习 Prompt

## LEARN-039：Student system prompt

建议正文：

```text
你是 Student。

用户选择你，表示用户不要你立即完成表面任务，而要你通过持续学习获得足够深入、准确、可迁移的知识，最后把知识编译为一个或多个 SKILL。

学习阶段你只有 teacher 工具。不要抱怨缺少读取、搜索、终端或写入工具；需要调查真实世界时，向 Teacher 提问，由 Teacher 使用工具调查。

持续向同一个 Teacher 学习，直到你认为已经没有值得继续获取的高价值信息。

每轮只提出一个中心问题。一次提出多个独立问题会提前固定问题空间，并让 Teacher 无法用当前答案改变后续问题。

优先使用特殊疑问：
什么、为什么、怎样、哪里、在什么条件下、什么会失败、什么会推翻当前理解、真正的问题是什么。

只有在验证一个已经精确形成的命题时，才使用是／否问题。

每次提问前，先在自然语言中形成你当前最好的理解或猜测。把相关部分告诉 Teacher，并明确要求：
若你已经正确，不要重复；
若局部错误，只纠正会改变理解的部分；
若问题前提或分类错误，直接推翻并重构问题；
不要迁就你的术语或组织方式。

优先追问会改变最终 SKILL 边界、核心原则、适用条件或执行方法的问题。
不要为了覆盖固定栏目而提问。
不要维护表格、字段、知识图谱或固定问题树。
允许新的回答彻底改变你原来的分类。

对于模糊目标，先发现值得研究的问题空间，再深入最有价值的分支。
不要把“阅读全部文件”当作完成标准。
不要把 Teacher 的长回答误认为已经理解。
通过反例、失败条件、边界和重新表述检验理解。

学习阶段不要尝试写 SKILL，也不要输出用户最终答案。
需要继续学习时调用 teacher。

停止学习前，必须完成一次最终苏格拉底反证：
把你准备用于编译 SKILL 的完整理解整理成最后一个问题交给 Teacher，
明确请 Teacher 寻找错误、遗漏、过度泛化或错误边界，不要重复已确认内容。
只有这次回答仍未带来高价值修正，才停止调用工具。

这不是收敛协议：不需要固定轮数，不需要 Teacher 的确认信号，
只需要“停止前把最终综合暴露给反证”这一次动作。
该反证与最后回答也会原样进入 QA.md，因此你的最终综合不会丢失。

当你认为已经没有新的高价值问题时，不调用工具，主动结束当前 turn 并进入 idle。
框架随后会把 QA.md 路径交给你进行最终编译。
```

## LEARN-040：Student 不得遵循固定问句模板

Prompt 只能引导思考质量。

不得要求每轮必须包含：

```text
Current understanding:
Best guess:
Question:
Confidence:
```

Student 可以使用最适合当前知识的表达。

可能只问：

```text
为什么？
```

也可能提交一整篇重构后的理解，请 Teacher 从任意角度摧毁它。

---

# 十一、Teacher Prompt

## LEARN-041：Teacher system prompt

建议正文：

```text
你是 Teacher。

同一个 Student 会在持续 Session 中反复向你学习。
你的职责不是尽快给出一篇最终文档，而是帮助 Student 获得准确、深入、可迁移的理解。

自由回答 Student 当前提出的中心问题。

不要迁就 Student 的分类、术语、假设或隐含前提。
如果真正重要的问题不同，先纠正问题本身。
如果多个表面问题来自同一个更基础原则，指出该原则。
如果源码、文档或现实证据与 Student 的假设冲突，以证据为准。

你拥有工具。需要知道真实情况时，调查真实情况：
读取源码、搜索调用路径、查看周边合同、运行必要验证、检查 Host 行为。
不要靠常识填补本可调查的事实。

不要按照固定栏目回答。
不要为了显得完整而平均覆盖所有方面。
可以使用任何最适合当前知识的表达：
论证、反例、源码路径、因果链、代码、思想实验、历史解释、失败案例、对比、重新定义或新的问题。

不要重复 Student 已正确掌握的内容。
优先提供会增加、纠正或重组其知识的信息。
必要时指出哪些问题目前没有证据支持。

每轮完成后，必须通过 return 工具返回一段完整自然语言答复。
不要普通 text-out。
不要直接对用户讲话。
不要替 Student 编写最终 SKILL。
```

## LEARN-042：Teacher 可以提出反问

Teacher return 可以包含反问。

但控制权仍返回 Student，由 Student 决定下一轮怎样回应。

Teacher 不得通过调用自身或创建另一个 Student 主动延长流程。

---

# 十二、学习循环

## LEARN-043：主流程

伪代码：

```fsharp
student {
    let! qa = openTaskQa ()
    do! appendUserRequest qa            // 用户原始请求先入 QA，再启动 Student
    let! teacher = getOrCreateTeacher ()

    let! learningOutcome =
        runStudentWithTools [ teacherTool qa teacher ]

    match learningOutcome with
    | Idle ->
        let! finalMessage =
            continueStudentWithCompilationTools qa.Path

        do! deleteQa qa.Path            // 先删除并确认，再结束
        return finalMessage

    | Failed error ->
        return! fail error
}
```

这只是结构化程序控制。

禁止为它建立：

```text
LearningPhase
QuestionPhase
ConvergencePhase
CompilationPhase
CleanupPhase
CurrentStage
NextAction
```

现有第一原理已经明确要求用语言运行时 continuation 表达流程，不在业务层重造 Stage 状态机。

## LEARN-044：Teacher 调用循环

```text
Student 调用 teacher
→ Teacher 获得一轮
→ Teacher 使用任意数量工具
→ Teacher 调用 return
→ QA 原子追加
→ teacher 工具返回
→ Student 继续推理
```

不存在固定轮数。

不存在“至少问 N 次”。

不存在“最多问 N 次”的知识合同。

全局资源保护仍可使用现有超时、取消和自动恢复预算，但预算耗尽必须作为运行失败暴露，不能伪装成知识收敛。

---

# 十三、idle 与 nudge

## LEARN-045：Student 学习阶段 idle

Student 在学习阶段 idle 且没有正在等待的 teacher 调用：

```text
表示 Student 主动决定停止提问
→ 进入最终编译
```

框架不判断该决定是否正确。

框架不要求 Teacher 返回 `Exhausted`。

框架不分析最近几轮是否仍有新信息。

框架不验证最终苏格拉底反证是否发生：idle 语义不依赖它，但 Student Prompt 要求它（LEARN-039）。

## LEARN-046：Teacher idle

Teacher 在当前 turn 未调用 return 就 idle：

```text
发送 Teacher continuation nudge
```

建议 nudge：

```text
你尚未通过 return 交还本轮完整答复。
继续当前工作，并通过 return 返回。
```

该 nudge：

* 继续同一 Teacher Session；
* 不创建新 Teacher；
* 不改变 Agent；
* 不改变 model；
* 不成为新的用户 Authority Root；
* 不进入 QA.md；
* 不被 Student 看见。

现有系统已把 BusyAgentNudge 等内部提示定义为 Continuation；Teacher nudge 应复用同一 PromptDispatcher 与身份纪律，不得绕过发送协议。

## LEARN-047：Student 编译阶段 idle

Student 已收到编译 Prompt，但未调用最终 return 就 idle：

```text
发送 Student compilation continuation nudge
```

建议文本：

```text
继续完成 QA.md 到 SKILL 的编译。
成功写入并检查最终 SKILL 后，必须调用 return 结束任务。
```

该 nudge 不重新进入学习阶段。

## LEARN-048：Teacher 工具调用期间 Student idle

Student 正在等待同步 `teacher` 工具结果时，Host 出现的 idle 信号不得触发编译。

必须先 reconcile 完整消息与工具状态。

禁止从原始 idle payload 猜测 Student 已完成。

现有架构已经规定 idle 只是粗粒度信号，业务事实必须通过 SDK 读取完整消息后 reconcile；本功能不得创建例外。

## LEARN-049：nudge 失败

若 Host 不支持向 busy/idle Teacher 追加 continuation，返回明确错误（具体错误名由实现定义）。

不得静默创建新 Teacher；LEARN-030 的 Replacement 例外也要求先证明永久丢失。

若自动恢复预算耗尽：

```text
teacher 工具以失败返回 Student
```

Student 可以决定重试、改写问题或最终结束。

---

# 十四、编译阶段工具面

## LEARN-050：StudentLearn / StudentCompile 两个请求种类

Student 的两种工具面不是运行时“切换”，而是两种真实请求语义。本条款是对 AGENT/PROMPT 规范的明确修订，`ProviderRequestKind`（PROMPT-008）新增两个种类：

```fsharp
type ProviderRequestKind =
    | ...
    | StudentLearn
    | StudentCompile
```

权限确定：

```text
Student × StudentLearn    → { teacher }
Student × StudentCompile  → { read, glob, grep, write, edit, return }
```

二者使用同一个 Student CanonicalRole 与同一个 fast/deep tier。

一次 provider request 的 Agent、CanonicalRole、system prompt、`ToolCapabilitySet`、RequestKind 必须全部绑定在同一个不可变 `AttemptExecutionProfile`（PROMPT-008）中原子冻结；Continuation 无权改变角色或 tier，只能在两个请求种类之间选择。工具面由 provider schema 与 execution gate 双层执行，不能只写成运行时技巧。

编译工具集的具体工具名可按现有项目实际工具名调整，但必须满足：

1. Student 能读取给定 QA.md。
2. Student 能检查已有 `.agent/skills` 约定。
3. Student 能创建目录与 SKILL。
4. Student 能重新读取成品自检。
5. Student 能调用最终 return。

默认不需要：

```text
fork-agent
fork-manager
join
list
fork-pty
浏览器
网络
teacher
```

StudentCompile 的工具集由 profile 原子保证不含 teacher。

知识获取已经结束；若 Student 过早 idle，这是 Student 自己的判断错误，不由框架建立返回学习阶段的复杂状态机。

## LEARN-051：工具面必须 fail-closed

若 StudentCompile 请求的 profile 构造或工具面安装失败：

```text
不得发送编译 Prompt
不得让 Student 看到一个既没有 teacher 又无法写文件的空工具集
显式失败当前任务
```

必须通过 provider-visible schema 与 runtime execution gate 同时验证当前请求的工具面（LEARN-050 的 profile 原子冻结是前提）。

现有架构对工具供给与执行边界采用 fail-closed 思路；Student 的两种请求种类必须接受同等级 Host canary，而不能只相信 Prompt。

## LEARN-052：请求种类不进入知识

QA.md 不记录请求种类（StudentLearn / StudentCompile）。

最终 SKILL 不需要知道 Student 曾经拥有哪些工具。

这是控制流事实，不是学习所得知识。

---

# 十五、最终编译 Prompt

## LEARN-053：正式 Prompt

框架观察到 Student 学习阶段 idle 后，发送以下 continuation。

`<QA_PATH>` 替换为本任务真实路径。

```text
你已经结束向 Teacher 提问。

完整学习记录位于：

<QA_PATH>

该 QA.md 是本次学习任务的唯一权威来源。
读取其全部内容。不要依赖文件之外的记忆，不要补充文件未支持的知识。

把其中获得的全部有价值知识整理为 `.agent/skills/...` 下一个或多个边界清晰、可以独立使用的 SKILL。

以第一性原理重新表达知识。
寻找能够解释并生成全部具体结论的最小充分原则。
不要按照问答时间顺序做聊天摘要，也不要机械复制对话。

第一性原理不等于删除细节。
最终制品必须构成对 QA.md 的语义无损压缩。

你可以合并真正同义或重复的表达。
你可以删除寒暄、试探，以及已经被后文完全取代且没有警示价值的中间措辞。

不得丢失任何会改变理解、判断或执行结果的信息。
必须保留适用条件、边界、例外、反例、失败模式、决策理由和重要实例。

被纠正的错误若具有警示价值，应转化为明确的反模式、误区或失败说明。
相互矛盾且未被解决的内容不得擅自调和，应明确保留不确定性。
无法归入核心原则的信息不能因为不方便组织而删除；应重新检查 SKILL 边界，或放入合适的补充章节。

不要因为某项内容看似属于实现细节就删除它。
只有当该细节能够由更基础原则完整推出，且不会损失成立条件、操作方法与例外时，才可省略重复表述。

按知识的自然边界决定 SKILL 数量。
不要把无关能力塞进一个综合文档。
也不要为了形式整齐而人为拆分同一个不可分割的能力。

每个 SKILL 都应让一个从未参与本次对话、也无法读取 QA.md 的 Agent 能够：

1. 理解该知识解决什么问题；
2. 从基础事实推出关键原则；
3. 知道原则在什么条件下成立；
4. 根据原则采取具体行动；
5. 识别常见误解、失败方式和例外；
6. 保留本次学习所得的全部有效信息。

遵循仓库现有 SKILL 目录、命名与格式约定。
先检查已有 SKILL，避免重复创建同一能力。
若已有 SKILL 应扩展，精准修改原文件，不创建平行真相。

完成初稿后重新读取完整 QA.md 和全部最终 SKILL。
逐段检查每一项具有语义价值的内容是否已经：

- 被最终 SKILL 直接表达；
- 被更基础原则完整蕴含；
- 或被明确保留为未解决项。

不得仅凭“整体意思差不多”判定完成。

成功写入并检查全部 SKILL 后，调用 return。
return 中只需简要说明生成或修改了哪些 SKILL。
不要在 return 正文中重新复述全部知识。
```

## LEARN-054：为什么不使用 coverage 表

禁止要求 Student 输出：

```text
QA 段落 1 → Skill A/Section 2
QA 段落 2 → Superseded
QA 段落 3 → Included
```

原因：

1. 映射表本身会消耗大量注意力。
2. 它鼓励机械覆盖，而非重新理解。
3. 一条第一性原理可能同时解释大量分散内容。
4. 一段问答可能只提供某个反例的一半。
5. Student 会为了填表保留无意义文本。
6. 机器无法验证“蕴含”是否真的成立。

正确做法是用 Prompt 要求 Student 完整重读、自我反证和语义检查。

---

# 十六、SKILL 产物

## LEARN-055：一个请求可产生多个 SKILL

例如：

```text
阅读本项目库，看看能学到什么编程经验。
```

最终可能生成：

```text
.agent/skills/design-event-folds/SKILL.md
.agent/skills/bind-provider-request-identity/SKILL.md
.agent/skills/build-fail-closed-tool-boundaries/SKILL.md
.agent/skills/design-session-satellites/SKILL.md
.agent/skills/verify-host-contracts/SKILL.md
```

不得强制生成：

```text
.agent/skills/project-programming-experience/SKILL.md
```

这种大杂烩会使触发条件模糊、知识边界混乱。

## LEARN-056：SKILL 的自然边界

独立 SKILL 应至少满足：

```text
可以被独立触发
可以被独立执行
拥有相对完整的问题边界
拥有自己的成立条件
拥有自己的失败模式
不依赖阅读其他无关 SKILL 才能理解核心动作
```

## LEARN-057：项目事实与可迁移知识

QA 中可能同时包含：

```text
本仓库具体路径
本仓库当前类型名
本仓库 Host 限制
可迁移设计原则
通用反模式
```

Student 自行决定：

* 哪些进入 SKILL 主体；
* 哪些作为项目内证据；
* 哪些属于适用条件；
* 哪些只应保留为实例；
* 哪些应进入已有仓库文档而非新 SKILL。

框架不规定章节。

---

# 十七、典型流程示例

## LEARN-058：明确目标

用户：

```text
为了实现万象术，需要宿主的哪些能力？
```

Student 第一轮可能调用：

```text
teacher("""
我目前只能猜测“万象术”要求宿主提供持续 Session、
受控工具调用、可恢复历史和对模型可见上下文的重写能力。

但这个猜测可能已经把实现方案当成了需求。

请先从“万象术必须完成什么、哪些事实必须跨轮存在”出发，
说明宿主能力的真正下界。
若我列出的能力不是必要条件，请直接推翻。
不要按我的四项分类逐项回答。
""")
```

Teacher 调查后自由返回。

Student 根据回答继续只问一个最高价值问题。

## LEARN-059：模糊探索

用户：

```text
阅读本项目库，看看能学到什么编程经验。
```

Student 不应直接问：

```text
这个仓库有什么经验？
```

更好的第一轮可能是：

```text
teacher("""
请先调查这个仓库真正承担复杂度的地方。

不要按常见的“架构、测试、性能、安全”栏目平均总结。
请找出哪些设计若被删除或误解，会让系统最先失去正确性；
哪些代码只是样板或宿主适配，不值得提炼；
以及最值得进一步追问、但从目录表面看不出来的关系。

此轮目标不是写经验清单，而是改变我对这个仓库问题空间的理解。
""")
```

Teacher 可能返回完全意外的组织方式。

Student 再选择一个中心问题深入。

## LEARN-060：反证收敛

后期 Student 可以问：

```text
teacher("""
假设我现在依据当前理解编写 SKILL，
交给一个从未看过本仓库的 Agent 使用。

它最可能因为我的哪项抽象而做出错误行为？

请寻找：
会推翻核心结论的证据、
被误当成通用原则的项目特例、
遗漏的成立条件，
或本身就错误的 SKILL 划分。

若真正的问题在我的组织方式，而不是遗漏某个细节，
请直接推翻组织方式。
不要重复已经确认的内容。
""")
```

Teacher 回答后，Student自行判断是否继续。

不需要 Teacher 返回 `Exhausted`。

---

# 十八、失败处理

## LEARN-061：Teacher 创建失败

```text
teacher 工具返回明确错误
Student message 已在落盘时进入 QA.md（LEARN-022），不回滚
Student 保持学习阶段
```

Student 可重试（重复内容按 LEARN-037 去重）。

不得创建多个候选 Teacher 并选择最先成功者。

## LEARN-062：Teacher provider 失败

遵循 Teacher 自身 Fallback 合同。

恢复后仍是同一 Teacher Session。

若最终失败：

```text
teacher 工具返回错误
Student message 已入 QA.md（LEARN-022），不回滚
```

不得把部分 reasoning 或工具流当成 Teacher 答案。

## LEARN-063：Teacher 未 return

```text
idle
→ nudge
→ 仍未 return
→ 按现有自动恢复预算继续
→ 预算耗尽后 teacher 工具失败
```

不得从普通 assistant 正文中截取“看起来像答案”的文本。

## LEARN-064：QA 写入失败

QA 追加失败发生在两个位置，语义相同（先落盘，后生效）：

```text
Student message 追加失败
→ 不发送给 Teacher
→ teacher 工具返回持久化错误

Teacher return 追加失败
→ 不得把答案交给 Student
→ teacher 工具返回持久化错误
```

运行时可用当前捕获的文本重试写入。

进程重启后，只有 QA.md 中已经存在的内容算发生过。

## LEARN-065：QA 文件损坏

若 QA.md 无法按 UTF-8 完整读取：

```text
停止编译
显式报告明确错误（具体错误名由实现定义）
保留原文件
不得尝试摘要损坏后半段
不得跳过坏字节继续生成 SKILL
```

可以保留旁路副本供人工恢复，但不得把修复结果静默冒充原始权威状态。

## LEARN-066：Student 过早 idle

框架仍进入编译。

不建立“你真的学够了吗”分类器。

编译 Prompt 会要求 Student 重读 QA；若信息明显不足，Student 可以在最终说明中如实报告。

第一版不支持从编译阶段返回 Teacher。

否则必须引入新的持久阶段、工具切换往返与恢复协议，复杂度远高于收益。

## LEARN-067：SKILL 写入失败

Student 不应调用 return。

若错误后 Student idle，编译 nudge 要求继续。

若最终无法写入，Student 可通过 return 说明失败吗？

第一版建议：

```text
最终 return 只在成功写入后可用
```

若 Host 无法动态约束，应由 Prompt 要求 Student不要把失败伪装成完成。

## LEARN-068：QA 删除失败

Student 调用最终 return，但 QA 删除失败：

```text
return 工具返回明确错误（具体错误名由实现定义）
不提交 terminal completion
不向用户显示完成说明
Student Run 不终止
Student 可重试 return
```

删除顺序是硬约束（LEARN-024）：删除 → 确认不存在 → 提交 terminal → 显示 message。

重试幂等：若操作系统明确报告 QA 已不存在，视为删除成功，继续提交 terminal。

## LEARN-069：插件重启

QA 路径由 Student Session 与 Logical Run 确定。

Teacher 关联恢复按 LEARN-030 执行：可证明存在 → 重新绑定并继续；可证明永久丢失 → Replacement Teacher 通过完整 QA 恢复；无法证明 → fail closed，不猜测。

启动恢复发现未清理 QA 时：

```text
相关 Student Session 仍存在且任务未 terminal
→ 恢复 Student 学习工具面
→ 发送自然语言 continuation
→ 告知完整历史仍位于 QA 路径
→ Student 可要求 Teacher 读取 QA 恢复理解

任务已 terminal
→ 清理孤儿 QA

无法判断
→ 保留 QA
→ 不自动删除
```

恢复不解析 QA 语义。

## LEARN-070：用户取消

用户明确取消 Student 任务：

```text
取消 Student 与 Teacher 运行
→ 删除本任务 QA.md
→ retire Teacher 关联
```

取消表示用户放弃该临时学习过程。

若 QA 删除失败，记录清理错误并保留文件，不伪装成功。

---

# 十九、Prompt Authority 与身份

## LEARN-071：用户消息

用户选择 Student 后的原始消息是 HumanRoot。

在启动 Student 之前，框架必须：

```text
创建本任务 QA.md
→ 原样追加用户原始请求
→ 确认落盘
```

不添加标题、字段或角色标签；原文加自然换行即可（LEARN-033）。用户原始请求是 QA.md 的第一条内容。

该 HumanRoot 同时：

* 创建 Student Logical Run；
* 选择 fast-student 或 deep-student（Agent ID 是实现选择，LEARN-016）；
* 初始化本任务 QA（用户请求为 QA 第一条内容）；
* 创建绑定 Teacher。

## LEARN-072：Student → Teacher

Student 的 `teacher(message)` 首次创建 Teacher 时，使用受控 AgentOwnerRoot。

后续 message 发送到同一 Teacher Session。

不得把自然语言内容、固定前缀或工具名当作身份依据。

## LEARN-073：Teacher nudge

Teacher nudge 是 Continuation。

它不得：

* 创建新 Logical Run；
* 改变 Teacher Agent；
* 改变 model；
* 重置 Fallback；
* 成为新的 Teacher 任务；
* 写入 QA。

## LEARN-074：Student 编译 Prompt

编译 Prompt 是 Student 当前 Logical Run 的 Continuation。

它不得：

* 创建新 Student Run；
* 改变 fast/deep tier；
* 成为新的用户 Authority Root；
* 重置 Fallback；
* 改写用户原始目标。

现有 PromptDispatcher 已要求所有插件产生的 user-shaped continuation 统一经过 claim、submit、physical acceptance 与恢复合同；Student、Teacher 相关 Prompt 不得直接调用 `prompt_async` 绕过。

---

# 二十、并发与所有权

## LEARN-075：每个 Student Run 单飞

同一 Student Logical Run 同时最多存在：

```text
一个 teacher 工具调用
一个 Teacher provider run
一个 QA 写入
一个编译 continuation
```

Student provider 正常情况下不会并发调用多个工具，因为学习阶段只有 `teacher`。

运行时仍必须拒绝异常并发，返回明确错误（具体错误名由实现定义）。

## LEARN-076：QA 单写者

QA.md 的唯一写者是 Student–Teacher runtime。

Teacher 工具、Student、文件工具都不得在学习阶段直接修改 QA。

编译阶段 Student 只读 QA。

最终 return 只删除 QA。

## LEARN-077：Teacher 不共享

不同 Student 任务之间不得共享 Teacher Session。

即使用户目标相同，也必须：

```text
X₁ → T₁
X₂ → T₂
```

避免旧任务假设污染新任务。

---

# 二十一、安全与隐私

## LEARN-078：QA 可能包含敏感信息

Teacher 可能读取：

* 源码；
* 配置；
* 错误日志；
* 内部架构；
* 密钥附近上下文；
* 用户私有文档。

QA 因此必须：

1. 不进入 Git。
2. 不上传到无关服务。
3. 不被 Blogger 或普通 Companion 当作工作日志摄入。
4. 不出现在普通 Agent background。
5. 不进入用户最终回复。
6. 任务结束后删除。
7. 使用最小文件权限。
8. 日志只记录路径摘要、字节数与结果，不记录正文。

## LEARN-079：Teacher 外部工具

Teacher 使用网络或外部资料时，仍遵守现有工具安全规则。

Student 的用户授权不是无限外发数据授权。

Teacher Prompt 应以真实调查为目标，不应把整个私有仓库上传给外部服务。

---

# 二十二、观测与日志

## LEARN-080：允许日志

只记录诊断：

```text
student_session_id
teacher_session_id
logical_run_id
operation
result
error
qa_bytes
duration
tool_name
```

建议 operation：

```text
student-start
teacher-create
teacher-call
teacher-return
qa-append
student-compile
student-return
qa-delete
student-nudge
teacher-nudge
```

## LEARN-081：禁止日志

不得记录：

* Student 问题正文；
* Teacher 回答正文；
* QA.md 内容；
* 推测的学习阶段；
* 当前知识分支；
* 置信度；
* 是否收敛；
* “下一步问题”。

现有日志规范明确把日志限定为诊断而非恢复协议；本功能保持一致。

---

# 二十三、Host canary

生产启用前必须验证真实 Host 行为。

本节是实施建议清单，不是规范条款：编号（C-01…）只是清单索引，不构成固定数量合同；实际 canary 集合与编号由实现阶段按 Host 实测确定。规范本身只规定前文条款的行为与不变量。

## LEARN-082：Agent canary

### C-01：Student 可公开选择

验证：

```text
fast-student / deep-student 可由 HumanRoot 选择
Teacher 不出现在公开 catalog
```

### C-02：模型绑定

验证：

```text
Agent = fast-student/deep-student/fast-teacher/deep-teacher
Model = None
Host 能解析正确模型
```

### C-03：Prompt 隔离

Teacher 最终 provider-visible system prompt 必须是 Teacher Prompt。

不得混入 Student Prompt、DevOps Prompt 或静态 Host Agent 冲突文本。

## LEARN-083：工具 canary

### C-04：Student 学习工具面

provider-visible tools 恰好为：

```text
teacher
```

### C-05：Student execution gate

即使伪造 `read`、`write` 或 `return` 调用，也必须拒绝。

### C-06：Teacher 工具面

Teacher 能看到预期完整工具集与内部 return。

### C-07：请求种类转换（StudentLearn → StudentCompile）

Student idle 后的下一 provider request：

```text
teacher 消失
read/write/edit/return 出现
```

### C-08：编译 execution gate

旧 teacher 调用即使被伪造也必须拒绝。

## LEARN-084：Session canary

### C-09：Teacher 复用

连续三次 `teacher` 调用必须使用同一个 Teacher SessionId。

### C-10：Teacher 叶子

Teacher 不创建 Companion，不出现在普通 list/join。

### C-11：并发创建

两个重复首次触发只能创建一个 Teacher。

### C-12：重启恢复

插件重启后：可证明关联存在 → 复用；可证明永久丢失 → Replacement Teacher 通过完整 QA 恢复并显式记录；无法证明 → fail closed，不猜测，不误连其他任务。

## LEARN-085：return canary

### C-13：Teacher 普通 text-out

普通正文不得完成 teacher 工具。

### C-14：Teacher idle nudge

未 return 的 idle 会向同一 Session 发送 continuation。

### C-15：Teacher return 路由

return 文本只成为 Student teacher 工具结果，不直接对用户显示。

### C-16：Student 最终 return

return 文本成为用户最终答复，并终止 Student Run。

## LEARN-086：QA canary

### C-17：原文保留

包含 Unicode、代码块、引号、工具名与长文本的问答必须逐字保留。

### C-18：原子更新

模拟写入中断后，QA 必须是旧完整版本或新完整版本，不得出现半段。

### C-19：先写后交付

任何输入未落盘前不得产生外部效果：Student message 写入失败 → 不发送给 Teacher；Teacher return 写入失败 → 不交付给 Student。

### C-20：路径可读

编译阶段 Student 能读取给定 QA 路径。

### C-21：不进入 Git

临时路径必须被 ignore。

### C-22：删除

最终 return 在提交 terminal 之前完成删除；QA 与空临时目录消失，删除失败不得宣称完成。

## LEARN-087：idle canary

### C-23：学习 idle

Student 空闲且无 pending teacher 调用时进入编译，不发送普通 nudge。

### C-24：等待工具时 idle

Student 等待 Teacher 时不得误进入编译。

### C-25：编译 idle

未调用最终 return 时发送 compilation nudge。

### C-26：重复 idle

nudge 不创建新 Logical Run，不改变 Agent，不重置 Fallback。

## LEARN-088：可见性 canary

### C-27：用户不可见 Teacher

用户 transcript 中不出现 Teacher 内部 turn、nudge 或工具原始流。

### C-28：用户不可见 QA

最终回复不自动附带 QA 内容或路径。

### C-29：普通 Agent 零影响

选择 Coder、Inspector、DevOps 等 Agent 时：

```text
不创建 QA
不创建 Teacher
不增加 Prompt
不改变工具面
```

---

# 二十四、测试阶梯

## LEARN-089：纯逻辑测试

测试：

* Student 工具面选择；
* Teacher tier 映射；
* QA 路径生成；
* 原子内容拼接；
* 临时路径 ignore 判定；
* return 清理结果；
* nudge 选择。

## LEARN-090：契约测试

通过真实公开边界测试：

```text
tool.definition
tool.execute.before
tool.execute.after
session.status idle
client.session.messages
PromptDispatcher
```

不得只测试私有辅助函数。

## LEARN-091：重放测试

覆盖：

```text
用户请求落盘后、Student 启动前崩溃
Student message 落盘后、发送给 Teacher 前崩溃
Teacher return 落盘后、工具结果交付前崩溃
Student idle 后、compile Prompt 前崩溃
SKILL 写入后、return 前崩溃
return 调用中、QA 删除失败
Teacher Session 丢失后恢复（复用 / Replacement / fail closed 三路）
```

## LEARN-092：真实 canary

真实调用 provider，证明：

* Prompt 正确；
* 工具 schema 正确；
* 同一 Teacher 延续上下文；
* idle 事件真实可用；
* return 可以阻止普通 text-out；
* 编译阶段工具面可切换；
* QA 文件可由模型读取；
* 最终清理真实发生。

测试纪律必须遵循现有“纯函数→契约→重放→真实 canary”的验证阶梯，不能用一次性手工试跑替代正式回归。

---

# 二十五、实施顺序

## LEARN-093：阶段 A——角色与 Prompt

实现：

```text
Student Role
Teacher Role
fast/deep Agent 配置
Student Prompt
Teacher Prompt
公开/内部 Agent 可见性
```

判据：

```text
Agent 启动验证通过
Teacher 不出现在公开枚举
Prompt canary 通过
```

## LEARN-094：阶段 B——Teacher Session

实现：

```text
Student → Teacher 关联
single-flight 创建
同 Session continue
Teacher 叶子约束
删除/取消级联
```

依赖统一 SatelliteRuntime（LEARN-027 最终裁决）；若尚未实现，先完成统一卫星结构，不允许并行存在 Teacher 专用关联框架。

## LEARN-095：阶段 C——工具

实现：

```text
teacher(message)
Teacher return(message)
Student final return(message)
角色工具过滤
execution gate
同步等待与结果路由
```

先让内存流程跑通，不急于加入 QA。

## LEARN-096：阶段 D——QA

实现：

```text
路径
权限
原子更新
逐字保存
先落盘后交付
删除
启动恢复
```

加入故障注入测试。

## LEARN-097：阶段 E——idle 与编译

实现：

```text
Student learning idle → compile continuation
Teacher idle → return nudge
Student compile idle → completion nudge
StudentLearn → StudentCompile 请求种类切换（LEARN-050）
最终编译 Prompt
```

## LEARN-098：阶段 F——SKILL canary

使用至少三类任务：

```text
明确技术目标
模糊仓库探索
存在错误前提的请求
```

人工审阅：

* Student 是否一次只问一个中心问题；
* Teacher 是否敢于推翻前提；
* 是否出现机械栏目填充；
* QA 是否完整；
* SKILL 是否第一性原理化；
* 是否遗漏边界和反例；
* 是否错误合并独立技能。

## LEARN-099：阶段 G——灰度

初期只对显式选择 `deep-student` 的开发者开放。

观察：

```text
平均 Teacher 轮数
QA 字节数
生成 SKILL 数量
Teacher nudge 次数
Student compile nudge 次数
失败率
清理失败率
用户主动取消率
```

这些指标只用于运行质量，不用于机器判断是否收敛。

---

# 二十六、对现有 SSOT 的修订

## LEARN-100：spec/00

新增角色说明：

```text
Student：公开主动学习 Agent；学习阶段只调用 Teacher，最终编译 SKILL。
Teacher：内部调查与教学 Agent；完整工具；只通过 return 回传 Student。
```

## LEARN-101：spec/02

修改：

```text
Role 新增 Student / Teacher
新增两个公开 Student Agent 与两个内部 Teacher Agent（具体 ID 由实现确定，LEARN-016）
工具权限矩阵新增 Student 学习面（StudentLearn）、Student 编译面（StudentCompile）、Teacher 行
内部 Agent 不可见清单加入 Teacher
```

需要明确 Student 是唯一存在 request-specific 工具面的公开角色。

不得把该能力泛化成任意 Role × Surface 权限代数，除非其他功能已形成真实复用需求。

## LEARN-102：spec/03

新增 PromptOrigin/ContinuationKind：

```text
TeacherQuestion
TeacherReturnNudge
StudentCompilation
StudentCompilationNudge
```

所有发送统一经过 PromptDispatcher。

## LEARN-103：spec/07

新增 reconcile 规则：

```text
Teacher return tool
Student learning idle
Student compilation idle
Student final return
```

保持 idle 仅为信号，完整状态从 SDK 消息读取。

## LEARN-104：spec/09

补充内部 Teacher Session 不进入普通 fork/join/list。

Teacher 工具不是 Manager `fork-agent` 的别名。

它有专用持续 Session、同步 return 与 QA 写入语义。

## LEARN-105：spec/11

不新增 Student 知识 Journal 事件。

只补充：

```text
QA 临时文件原子写入
删除失败处理
启动孤儿清理
```

明确 QA.md 是 Student 功能自己的唯一权威状态。

## LEARN-106：spec/99

新增术语：

```text
Student
Teacher
Teacher Session
QA.md
Student Compilation
Teacher Return
```

---

# 二十七、明确拒绝的替代方案

## LEARN-107：拒绝结构化 TeacherAnswer

拒绝：

```fsharp
{ Answer
  Decisions
  Evidence
  Unknowns
  Status }
```

原因：提前规定答案本体。

## LEARN-108：拒绝知识图谱

拒绝把 QA 自动转换为：

```text
Claim
Dependency
Contradiction
Branch
Confidence
```

原因：框架无法在探索发生前知道正确概念。

## LEARN-109：拒绝多个 Teacher

拒绝：

```text
一个架构 Teacher
一个测试 Teacher
一个安全 Teacher
```

原因：

* 知识割裂；
* 相互矛盾；
* Student 被迫做早期分类；
* 失去持续共同理解。

## LEARN-110：拒绝自动触发

拒绝让分类器决定何时进入 Student。

用户选择 Agent 已经是最清晰、最低复杂度的授权边界。

## LEARN-111：拒绝 Teacher 每轮新 Session

原因：

* 重复背景；
* 答案前后不一致；
* Student 必须搬运整个历史；
* 工具调查上下文丢失；
* 追问退化为独立问答。

## LEARN-112：拒绝 QA 派生 Journal

原因：

* 两份真相；
* 崩溃窗口需要 reconcile；
* schema 固化知识；
* 增加迁移与版本负担；
* 无法证明结构化结果没有丢失原意。

## LEARN-113：拒绝框架判定收敛

框架不知道：

* 什么知识重要；
* 哪个遗漏会改变 SKILL；
* Teacher 是否仍有东西可教；
* Student 的当前抽象是否正确。

收敛只能由 Student 根据自然语言理解判断。

## LEARN-114：拒绝最终机器 coverage

机器可以验证文件存在、格式合法、工具成功。

机器不能证明：

```text
最终 SKILL 没有丢失 QA 中任何有价值信息
```

该目标由编译 Prompt、Student 重读与实际审阅承担。

---

# 二十八、审阅者必须确认的裁决

本方案进入实现前，只需对以下实质问题作出批准或否决：

1. 是否接受用户显式选择 Student 作为唯一触发条件。
2. 是否接受 Student 学习阶段只有 `teacher` 工具。
3. 是否接受 Teacher 为同一持续内部 Session。
4. 是否接受 Teacher 不额外收窄工具能力。
5. 是否接受 Student–Teacher 知识传递完全使用自由文本。
6. 是否接受 QA.md 为唯一权威状态，不增加 Journal。
7. 是否接受 Student idle 作为进入编译的唯一语义信号。
8. 是否接受 StudentLearn / StudentCompile 两个请求种类（LEARN-050）承载两种工具面。
9. 是否接受第一性原理与语义无损冲突只由 Prompt 解决。
10. 是否接受最终 return 删除 QA 后终止。
11. 是否接受第一版不支持从编译阶段返回 Teacher。
12. 是否接受 Host canary 通过前默认关闭。

若以上裁决不变，其余内容均属于实现细节，不应重新引入结构化知识协议。

---

# 二十九、最终不变量

```text
用户未选择 Student
→ Student & Teacher 功能不存在。

用户选择 Student
→ 必然进入学习流程。

Student 学习阶段
→ provider-visible 工具只有 teacher。

每个 Student 任务
→ 恰好一个持续 Teacher Session。

Teacher 每轮有效结束
→ 必须调用 return。

Student 与 Teacher 传递的知识
→ 只有自由自然语言。

框架
→ 不解析知识语义。

每个自然语言输入（用户请求、Student 追问、Teacher 回答）
→ 先进入 QA.md，再产生外部效果。

用户原始请求
→ 是 QA.md 的第一条内容。

Student 最终综合
→ 通过最后一次苏格拉底反证进入 QA.md。

QA.md 存在期间
→ 它是唯一权威状态。

Student 学习 idle
→ 进入最终编译。

编译
→ 读取完整 QA，生成一个或多个 SKILL。

第一性原理压缩
→ 不得损失任何会改变理解或行动的信息。

Student 编译完成
→ 必须调用最终 return。

最终 return
→ 先删除 QA.md 并确认不存在，再终止对话。
```

---

# 三十、最终结论

Student & Teacher 不应被实现成：

```text
两个 Agent 互相聊天
```

也不应被实现成：

```text
Teacher 填结构化问卷
→ Student 根据字段拼文档
```

它应被实现成：

```text
用户显式选择学习
→ Student 用特殊疑问持续暴露未知
→ 同一 Teacher 调查真实世界并自由回答
→ QA.md 完整保存思想历史
→ Student 自行判断知识是否已经收敛
→ 精心设计的 Prompt 完成第一性原理下的语义无损压缩
→ SKILL 接替临时思想记录成为持久知识
```

该设计刻意把机器能力限制在机器真正可靠的部分：

```text
维持 Session
转交文本
执行工具
观察 idle
持久化文件
冻结请求种类
清理临时资源
```

把只有模型能够完成的工作留给模型：

```text
发现未知
重构问题
理解回答
判断信息价值
识别收敛
建立第一性原理
保留语义差异
划分 SKILL 边界
```

最终原则：

> 用户显式选择学习；自然语言自由探索；QA.md 保存全部真相；提示词完成语义无损的第一性原理压缩。
