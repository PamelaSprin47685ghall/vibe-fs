本文件只规定 Agent 如何查找规范、修改仓库和验证交付。产品语义只由 `requirements/<package>/` 定义。本文件引用条款，不复述条款。

## Kolmogorov 原则与工程手艺

写代码如同造器物。器物好用，是因为结构清楚、分工明白、没有多余的摆设。好代码每一行都在解决实际问题：名字说的是实在的事物，分支对应实际的业务情况，类型则把不合规的差错挡在门外。

- **追求简单明了**：如果一个函数或文件写得太长、太绕，通常不是业务本身真有那么深奥，而是塞进了太多多余的样板、框架礼仪和绕圈子的抽象。好设计把不可减少的业务说清楚，不多费读者一句口舌。
- **划清边界，不乱套用**：两处代码长得像，不等于就是同一种事物。生命周期不同的概念，即使眼下字段一样，也要各自分开。同一个用户在登录、账单、权限和展示里关注的信息完全不同，在各自的语境里只传真正需要的数据，不要试图造一个无所不包的“大万能对象”。
- **用类型系统把关**：字符、数字、布尔值最容易混淆。订单号、用户号如果都写成普通的字符串，编译器就分不清。给独立的领域概念各自起好类型名字，运行时代价极低，却能让编译器在门前把关。有限的状态就用明确的联合类型列出来，不靠一堆布尔开关在运行时互相猜。
- **纯计算与外部操作分开**：纯函数不读时钟、不掷骰子、不查网络、不写磁盘，同样的输入一定给同样的输出。这部分最容易测试，最让人踏实。把读写文件、网络通信和时序操作留在薄薄的外壳层，由外壳负责调度和持久化。
- **分清意图与事实**：用户命令是“想要做什么”，系统检查规则，通不过就拒绝；事件则是“已经做成的事实”，发生了就不能赖掉。银行靠流水算出余额，系统靠不可篡改的事件还原历史。不要直接在原字段上覆盖涂改旧数据，历史需要诚实追加。
- **围绕所有权处理并发**：并发最怕的是多方同时改同一块状态。让每个处理单元拥有自己的状态，外界只发消息，内部按序处理，就不需要到处上锁。
- **持久化诚实可靠**：收到请求先落盘，写入成功后再更新内存，不能让内存看见没有证据的未来。文本日志一行一个自包含记录，追加只在末尾，恢复时按序重放。
- **追查根因，不做糊弄修补**：工具报错和测试失败是线索，不是噪音。动手改代码前，先顺着调用链摸清原委，找到真正管事的模块和破坏的不变量。消除错误靠因果解释和正式回归测试，不靠猜谜式试错。
- **文档与测试就是手艺的记忆**：踩过的坑若不记下来，以后还会掉进去。改动涉及协议边界时，必须补上正式的自动化回归测试。随手在控制台打印、跑完即删的临时脚本不算验证。
- **名字是给人读的**：名字要反映代码所依赖的真实概念。不用让人猜谜的生僻缩写，不用故弄玄虚的数学黑话，注释只留给真正隐晦难明之处，不写重复代码字面意思的废话。
- **测试要有说服力**：先写能证明问题的失败测试，再把代码改对。测试断言的是对外的公开行为，不是内部具体先调哪个辅助函数。不为图省事而削弱断言，不靠碰运气重跑，也不靠放大超时来掩盖时序漏洞。
- **范围克制，不留尾巴**：把托付的事改得清清爽爽。不以顺手为由重构无关模块，但也不在改动范围里留下废弃代码、调试打印和未清理的死分支。

## 思考与输出

说话与写作要直截了当，不端官架子，不搞仪式感表演。凡事拿不准时，怎么简单明白就怎么来。
不要搞生硬的翻译腔和居高临下的教材腔，文字多向费孝通先生的直白精准、从小处着眼，以及小平邦彦先生的自然顺畅靠拢。

## 工具调用纪律

- 只要动作彼此独立，就尽量并行读取、调查与验证。并发是为了提高效率、理清因果，不是为了盲目追求工具数量。
- 在同一文件上有重叠的编辑、存在先后依赖的修改，或者依赖上一条命令结果的执行，必须串行完成。
- 跨文件修改先判断依赖关系：修改公共类型或接口时，先定好契约边界，再调整调用方，最后清理旧路径。
- 精准修改局部代码，拒绝无谓的全量重写。
- 把大诉求拆成细小、明确的独立步骤，避免长时停滞。

## 架构与编码风格

- 崇尚朴素、简洁、直白的架构，去除不必要的过度设计，拒绝复杂的错误包装、日志堆砌和配置层级。
- 除非绝对必要，不写多余注释。不搞糊弄过关的临时补丁，想清楚再动手。
- 避免冗余的中间赋值，边界清晰即可，不引起阅读负担。
- 严禁通过一行写多件事或滥用分号来伪造行数减少。
- 变量命名务求明白晓畅，绝不用晦涩名字和让人费解的缩写。
- 重构时痛快甩掉历史包袱，不滥用外观模式（facade）逃避清理；不保留已废止的代码，不合理之处随时与下游协同修改。
- 精准实现，不搞看似保险其实谁也说不清原理的兜底实现。

## 具体工作与流程

- 严禁使用 `dotnet build`。本仓只有 Fable 编译目标，构建必须使用 `node scripts/build.mjs` 或 `npm run format-build-test`，严禁引入或依赖 .NET 编译构建。
- 宁慢且稳，严禁使用自动化程序批量增删改查程序代码。手工编辑，步步为营。慢即是快。

1. 工作流程
  - 普通小型修复、重构与测试补充不要求创建 Change；在单次提交内原子闭环。
  - 基本闭环：弄清为什么做（why）→ 明确要做成什么样（what）→ 阅读相关规范理解依据 → 调整与补充测试 → 落实实现（how）→ 检查所有相关测试全绿 → 闭环结束。
2. 两种典型失败
  - 写完代码才想起看文档：代码已经按旧想法写死，要么彻底返工，要么硬把旧语义塞进系统，导致代码与规范越走越远。
  - 陷在局部细节里丢了大局：虽然修好了表面报错，却违背了整体规则（例如只给旧类型打补丁加字段、加适配器凑合，合起来就是在维护混乱）。
3. 交付门禁
  - 涉及行为、持久化、Host/provider、Git 或跨包边界时，跑对应 requirement 测试套件，尽量不全量漫跑。
  - 测试文件按 WHAT 条款编号统一命名为 `NNN.test.mjs`；需求与测试的映射由测试用例标题中的 `WHAT[PREFIX-NNN]` 锚点定义。
  - 提交前确认没有残留的临时文件、调试打印、旧路径残骸或未跟踪的生成物。
4. 修改纪律
  - 工作区可能包含用户的未提交改动。动手前先看 `git status` 与对应 diff，妥善保留无关改动。
  - 自动提交 git commit 并推送到 `master` 分支；严禁对 `master` 执行 force push。

---

# PROMPT-006 执行绑定去层化：实施稿

基线：`~/Desktop/vibe/wanxiangshu`，审阅时 `master = ab62b7d2f`，工作区干净。

交付边界：实施设计，不是实施授权。本次未修改仓库，未运行构建、测试、真实 Host canary、提交或部署。以下代码片段是拟定接口或流程示意，不代表已经实现、编译通过。

目标：删除 Host 层重复保存的身份与模型绑定，让合法执行不再依赖“另一张表是否及时登记、恢复、清理”。减少的是状态、写入口和交接，不是错误提示的数量。

---

## 1. 先把本次做什么定死

### 1.1 完成后的结构

```text
插件发送：已有 authority / IdentitySeed → durable claim → Host transport
真实接收：wire 解码 → 身份与来源判定 → durable Accepted → acquire → project → commit
后续 hook：exact execution key → Accepted evidence + 已提交的租约
执行结算：已有执行所有者 + failure policy → durable terminal → exact 资源结算
```

发送和真实接收是不同物理边界，不能为了画出一条短链而合成一个“成功”。`ProviderStarted` 仍只能由真实 Host provider-start evidence 建立，不能提前伪造。

### 1.2 本次采用的决定

1. **不新建 `ReadyExecution` 管理层、通用事务引擎、事件总线或“总协调器”。** 使用已有 Accepted evidence、租约和 runtime。
2. **保留 `acquire / commit`。** 现有容量层的 Pending / Committed 是实际资源状态，不是样板。删除它们外面的层层包装。
3. **准入编排暂留 `OpenCode/Host/ChatAdmission/Transaction.fs`。** 语义上服从 managed-chat-execution 所有者；不把 Host 类型和 ModelRouting 依赖移进纯执行模块。
4. **新查询必须真正只读。** 不用现有 `tryLease` 冒充查询，不用 `take*`、`consume*` 当 peek。
5. **不新增身份事实来源。** participant / role / persona 从已有 identity evidence 读取；模型目标和容量状态由 ModelRouting 提供。
6. **不改历史事件，不改变重试策略。** crash recovery 不因本次重构获得自动重发或重放工具的权力。

### 1.3 明确不做

不重写容量调度、借贷公平性、provider retry/fallback、Companion 生命周期、fission 生命周期、Persona 规则或整个 Host adapter。保留 Strength 既有的非等待预约语义；不把其预约误当成已经 Accepted 的物理执行。

不以“少写代码”为由：忽略真实身份漂移、取消 fence、把未知发送结果当成没发送、把未知持久化结果当成没落盘。

---

## 2. 必须记住的已核实陷阱

下列源码路径省略 `src/Wanxiangshu/`。

| 已核实事实 | 对施工的影响 |
|---|---|
| `ModelRouting.tryLease` 在未命中时可能分配、接管预约或替换现有租约。见 `OpenCode/Host/ModelRouting.fs:1176–1216`。 | 必须先增加真正的 exact 只读查询，再迁 hook；不可直接复用该函数。 |
| `SessionExecutionBinding` 除绑定表外，还承担 provider step 交接、AttemptPlan 冻结和 ProviderStarted 持久化。见其 `.fsi`。 | 先迁有效职责，再删除文件；不是一键删整个模块。 |
| Dispatcher 已从 IdentitySeed / profile 取得 agent，但 `SendAgentOwnerRootWithTools` 仍接受可选 model，并写入发送 options。见 `Interaction/Dispatch/Send.fs:334–384,454–476`。 | 不需要新增发送凭证；但必须先清理 model 参数，否则删除下游 `prepare*` 后可能改变发送语义。 |
| `Transaction.fs` 包装了 Accepted/Leased/Targeted/Bound/Projected；生产调用只匹配 `Settled _`。见 `Transaction.fs:104–134`、`HostSignalBootstrap.fs:637–650`。 | 可以删中间对象和成功回执；不能误删真正的持久化确认及租约能力。 |
| 工具入口当前只向绑定层传 session 和 ProviderRunId。见 `OpenCode/Tools/ToolRegistry.fs:361–368`。 | 迁工具边界前必须取得该 run 的精确 physical parent，不能拿当前 session 绑定代替。 |
| 已有真实 Host canary 检查 params 的物理 message ID、assistant parentID、重复 messageID 投递。见 `requirements/host-boundary/tests/023.test.mjs`。 | 复用并扩展；本次没有运行它，源码存在不等于当前安装环境已通过。 |
| 道路投影有 `BoundDevOpsModelTarget: string option`。见 `Mission/Relay/Fold.fs`。 | 存在这个字段不等于已证明能完整恢复 provider/model/reasoning；删除重复 DevOps 表前必须跑通完整恢复证据链。 |

---

## 3. 先改规范，避免代码简化后被旧门禁逼回原形

保留现有条款编号，局部修改 WHAT / HOW / APPLIES-TO 与对应测试。不要为这次重构再建一个平行规范体系。

| 规范 | 本次修改点 | 不得改变 |
|---|---|---|
| `managed-chat-execution` [001/003/009/011] | 明确 exact key；binding 指容量所有者持有的 exact lease 身份，不再要求 Host 第二次注册；描述新准入顺序。 | durable Accepted 先于容量准入和 Host 修改；进程资源不持久化。 |
| `execution-model-routing` [009/011/012] | 后续 hook 从 owner 只读查询；清理 [011] 中“先解析目标”与 Accepted 前置的表述歧义。普通执行的 scheduler/acquire 放在 Accepted 之后。 | exact fence、目标稳定性、用途与资源身份。Strength 预约例外按原所属规范保留，不默默扩大。 |
| `dispatch-protocol` [010/012] | 发送 agent 直接由已有证据投影；managed send 接口不接受 model；不要求 SessionExecutionBinding 存在。 | PromptKey、claim、真实 PhysicalAccepted、未知结果不得重发。 |
| `crash-reconciliation` [020/021] | 把“必须回填绑定缓存”的实现要求改为“无该缓存仍能依据权威事实正确发送/查询”。 | 子会话、Companion、fission、固定 DevOps 的真实生命周期和恢复语义。 |
| `host-boundary` [006/008/019/023] | 规定各 hook 的 exact 关联与只读边界；保留真实 Host 版本证据。 | 不从未来 assistant 推导身份，不靠轮询等待补证据。 |
| `session-ontology` 相关 HOW / APPLIES-TO | 移除被删除模块的管辖路径；拓扑查询留在拓扑所有者。 | 不把物理 parent 当成授权证据。 |

还要清理 `interaction-authority` 中“外部显式名称缺失如何处理”的规范表达：保留当前允许的 durable authority 解析规则，明确由权威入口处理；不能简单删除所有缺失字段支持，也不能把 Host/session 缓存默认值包装成显式用户输入。

**规范验收：** 新文档不再要求 `acceptPromptExecution`、`observeUserFacingAgent`、`exactExecutionBindingCount` 等旧实现必须存在，但原有业务安全断言仍完整。

---

## 4. 施工顺序总表

| 批次 | 产物 | 前置条件 |
|---|---|---|
| P0 | 基线、规范变更、真实 Host 关联验证与失败测试 | 无 |
| P1 | exact 只读租约查询 | P0 |
| P2 | params / transform / provider-start / tool 消费端迁移 | P1；相关 Host 证据通过 |
| P3 | 发送端直接使用已有 authority，移除 model/BindingIntent 补齐路径 | P0；调用点清点完成 |
| P4 | 准入直线化，删除 Bind / Unbind 和步骤回执 | P2、P3 |
| P5 | 恢复与清理职责归位，彻底删除旧绑定模块 | P4；DevOps 恢复证明通过 |
| P6 | 执行投影改为互斥状态，完成全套收尾验证 | P5；快照兼容边界核实 |

P2 与 P3 的调查和测试设计可以并行；重叠文件编辑串行。每个代码批次交付时必须可构建、对应测试通过。中间版本可以暂时保留旧调用者，但不能为新方案再双写一套身份表；最后不得保留“先兼容一下”的旧入口。

---

## P0：基线和第一道门槛

### 改动前

确认 HEAD、`git status` 和实际 diff；工作区变化则重新确定基线，不能覆盖其他人的改动。读取工作路径下生效的 AGENTS。

先用当前代码建立针对性测试基线，记录失败标题、命令和日志位置。既有失败不能通过重跑、扩大超时或降低断言消失。新增期望失败测试可先在本地证明问题，但与对应实现同批交付；不要把一批已知失败的新测试单独推入主分支。

### 先查明 Host 的 exact 关联

扩展现有 `host-boundary/tests/023.test.mjs` 及其 support canary，核实：

- `chat.params` 的 `message.id` 真的是本次 physical user-message ID，不是 assistant ID。
- transform 的尾部 user message 与本次请求一致；缺失或多个相互冲突 carrier 时拒绝。
- 每个 assistant ProviderRun 的真实 `parentID` 能关联到对应物理用户消息。
- tool context 的 ProviderRunId 能找到该 run 的 exact parent；同一 physical 的第二次、第三次 provider run 也能找到。
- 拒绝准入后不会仍然到达 provider body；ProviderStarted 的既有持久化前置仍成立。
- 重复 messageID、工具后续 step、Host 辅助请求、同一 session 新旧消息交错分别如何到达 hook。

不要只验证一轮无工具对话。不要把测试里手填正确 ID 当成真实 Host 证明。

**门槛：** 若某个必需回调没有公开的 exact 证据，相关切换暂停；可继续 P1 和无关纯计算工作。先补 Host 公共适配能力或明确受支持版本，禁止加 session-current 回退。未通过真实 canary 不宣告上线就绪。

---

## P1：增加真正只读的 exact 租约查询

### 修改文件

- `OpenCode/Host/ModelCapacity/Surface.fs/.fsi`
- `OpenCode/Host/ModelRouting.fs/.fsi`
- 相应 `ModelRoutingSurface` 测试出口
- `requirements/execution-model-routing/tests/009.test.mjs`、`012.test.mjs`

### 拟定接口

```fsharp
// 新接口草图：不额外建立一张查询表。
val internal tryReadExecution:
    key: ChatExecutionKey -> ExecutionAdmissionLease option
```

在现有容量所有者的 lifecycle 表上读取，按 session 找到条目后必须继续核对 physical ID 和阶段；只返回仍为 Committed 的 exact lease。必要的 routing 数据一致性核对仍在所有者内部完成。Pending、Released、wrong-physical、未知条目不得被当成可执行租约。

查询不得调用 scheduler，不得接管预约，不得发放新 fence，不得 commit、release、drain queue，不得通过读取增加 duplicate/stale/conflict 计数。返回已有不透明租约，不另造 `ExecutionSnapshotRegistry`。

租约中的 participant、role 是资源身份的组成部分，仍需与 Accepted evidence 一致；它们不是第二套可独立选择的用户身份。Purpose 从准入时建立的资源状态取得，查询不能重新猜 `Normal`。

### 必须先写的测试

同一个只读查询执行多次前后：scheduler 调用次数、资源 ledger、队列、fence 与 owner 状态不变。分别覆盖命中、未命中、Pending、Released、错误 physical、旧 fence、新消息 supersede。

特别测试：存在 Strength reservation 时查询不得将其升级成 physical execution；B 消息存在时查 A 不得返回 B 的目标。

### 交付条件

只读查询通过生产 ModelRouting/Capacity Surface 测试。旧 `tryLease` 不再出现在任何新验证路径；此时不急于删除旧函数，先迁完调用者。

---

## P2：先迁读取端，让 hook 不再依赖绑定表

### 2.1 `chat.params`

修改 `ChatParamsHook.fs/.fsi` 及创建它的 composition wiring。注入现有执行投影的 exact 读取能力，不再偷偷读全局 journal，也不由 hook 管理恢复。

处理顺序：

```text
按已验证 Host 合同解码 exact key 和实际模型
→ 读取该 key 的 canonical execution
→ 读取该 key 的 committed lease
→ 核对 actual agent / actual target 与已有证据
→ 仅执行既有允许的参数投影，例如 temperature
```

删除 `observeUserFacingAgent` 调用。不能因为 owner 查询为空就归为 HostInternal；受管输入缺证据应明确拒绝。只有独立的、正式的 HostInternal/辅助请求证据才允许走其既有路径。

model 身份从真实 resolved model 读取；reasoning/variant 的 carrier 按通过 canary 的 wire 合同处理。不要用“期望的 variant”填补缺失的实际观察，否则检查会永远成功。字符串等价规则集中在一个 codec/比较函数；不在本次重构顺手扩大大小写或 legacy 名称接受范围。

### 2.2 transform 和 provider step

修改 `PluginTransforms.fs`。把原 `beginPhysicalProviderAttemptForTransform` 中的事情拆清：quiescence 与 step-entry 保留；clear/rebind ExpectedBinding 删除。exact key 从本次真实 messages 得到，不读 session 最新值。

以现有 ModelRouting step API 进入容量阶段。重复 transform 复用已有的 exact 幂等身份；同一 physical 的下一次 provider step 与重复通知必须可区分，不能只用 SessionId 或 PhysicalUserMessageId 永久去重。

容量排队、借贷、lender 召回的算法本次不改。

### 2.3 AttemptPlan 和 ProviderStarted

`freezeProviderAttemptPlanForTransform` 与 `persistProviderStartedFromObservation` 不是绑定缓存，应保留。

优先把 Host-specific helper 放回现有 `ChatAdmission` 集成目录；若放进调用文件会造成明显重复，可增加一个 `ProviderLifecycle.fs/.fsi`，只容纳这些现有逻辑，不增加字典或独立状态机。纯决策继续复用 AttemptPlanner 和 managed-chat lifecycle owner。

保留：首次精确 plan 冻结、真实 run 出现后绑定、同一 physical 的 ProviderStarted 单次建立、terminal 后不得首次启动。后续工具触发的新 assistant run 不要求第二份 Accepted 或第二个 ProviderStarted。

### 2.4 工具边界

修改 `OpenCode/Tools/ToolRegistry.fs` 及实际构造 HostToolContext 的 codec/wiring。

优先在 Host adapter 构造 context 时，通过公开 assistant evidence 提供该 ProviderRun 的 exact physical parent。已有 exact run-to-message 证据读取可复用；禁止用 `currentProviderModel`、最新 session binding 或“最后一条用户消息”替代。

AttemptPlan 只有在已证明覆盖该 run 时才能帮助关联；不能假定首个 plan 覆盖后续所有 provider run。缺少公开证据时回到 P0 的契约门槛，不另起一个猜测型登记表。

释放/结束 step 仍在任何业务 tool body、role gate、可能等待后代 provider 的工作之前。旧 tool event 不得结束新执行的 step。不要把 tool-calls 当作整个 physical execution 的 terminal。

### 2.5 其他只读消费者

`BloggerChronicleText.fs` 当前使用 `currentProviderModel(session)`：改为接收该材料所属 execution/run 的 exact context，或读取其已完成的真实记录。没有 exact 信息时明确无该信息，不能继续显示另一次执行的“当前模型”。

### 测试与交付

覆盖相同物理消息的重复 hooks、多步工具、A/B 交错、终态后迟到事件、HostInternal 不建立受管租约。真实 Host canary 与 `host-boundary` [006/008/019/023]、`execution-model-routing` [009/010/012] 测试通过。

本批完成后，生产读取端不再依赖 `providerAttemptBindings` 或 `acceptedPromptBindings`；旧写入端暂留，等待 P4 删除。不得新增第二份相同绑定状态。

---

## P3：发送端直用 authority，不再按会话形态补 agent

### 修改文件

- `Interaction/Dispatch/Send.fs/.fsi`
- `Interaction/Dispatch/DispatchSessionPort.fs/.fsi`，只在实际接口需要收紧时改
- `OpenCode/Host/Sessions.fs`
- `Strength/Replica/Runtime.fs`
- `Execution/Delegation/SyncDelegate/Runtime.fs`
- 相关 JS Surface
- `HostSignalBootstrap.fs`、`PluginSessionWiring.fs` 和 ingress codec 调用点

### 具体动作

**先处理发送的 model 参数。** 移除 `SendAgentOwnerRootCore` 和 `SendAgentOwnerRootWithTools` 的可选 model 参数；相关调用同步调整。managed send 构造 options 时固定 `Model=None`。底层公共 Host transport 是否支持 model 是另一回事，不能一并删除所有非 managed 用途。

Strength 的预约目标仍由 ModelRouting 持有，chat.message 按既有语义接管；不再通过 prompt 的 model 字段走捷径。测试必须证明预约没有丢失、没有二次调度、没有重复计数。readonly-delegate 的 purpose 仍来自已授权的真实请求/委托证据，不从 agent 字符串猜。

**直接投影已有身份。** Root 使用已有 validated IdentitySeed，Continuation 使用已有 active profile。删除 `prepareManagedPrompt / prepareUserFacingPrompt` 这条再次查询和校验缓存的链。不要另造 SendPermit、PreparedIdentity 等包装。

`BindingIntent` 只为旧补齐流程服务的部分删除；清点所有调用，确实无其他契约后从 options 和 JS Surface 删除该字段。不要保留始终填 `Preserve` 的死参数。

`Sessions` 不再决定 participant，只处理 transport 和真实会话生命周期。必要的 child/fission/Host auxiliary 创建、继承语言和清理职责仍保留。

**外部输入不能与内部发送混为一谈。** wire codec 保留用户实际提供的 agent；字段缺失就保持缺失，由 authority ingress 按已有合法规则解析 durable active/history。不能先从缓存补出一个字符串，再把它称为 ExplicitAgent。没有合法证据时拒绝，不填默认 manager/engineer。

### 测试与交付

新 Root 在没有 session agent 缓存时可正常发送；Continuation 精确继承原 run；错误显式 agent 仍拒绝；发送一律 model-free。缺 agent 的真实用户输入在现有允许场景中不回归；历史身份不能静默升级为新权限。

删除下游 `prepare*` 前必须看到 Strength / SyncDelegate 的调用测试通过，否则先停在此处。

---

## P4：准入直线化，删除独立 Bind / Unbind

### 修改文件

- `OpenCode/Host/ChatAdmission/Intent.fs/.fsi`
- `OpenCode/Host/ChatAdmission/Transaction.fs/.fsi`
- `OpenCode/Host/HostSignalBootstrap.fs`
- `OpenCode/Host/PluginHostInterop.fs`
- `ChatAdmission/*Surface.fs/.fsi`

### 4.1 让入口只接收真正的 managed intent

现在 Transaction 接收包含 HostInternal/Reject 等分支的完整 Decision，再在内部 `invalidArg`。将三种 managed 分支收成一个明确的 `ManagedIntent` 输入，外层分类一次后再调用。

只增加这个确实排除非法输入的业务类型，不为每个函数阶段加一种类型。exact key 可从 intent 取得，不在调用者和 Transaction 里各写一套匹配。已有重复 key record 可在不引入依赖环的前提下统一为 `ChatExecutionKey`。

删除输入的 `CurrentState`。在 owner 的正常协调边界按本次 exact key 读取状态，不接受调用者带来的、可能过时的状态副本。

### 4.2 一条成功路径

```text
读取 exact 状态并处理已结束/已开始/重复请求
→ durable accept（已有 witness）
→ acquire（已有 lease）
→ 等待返回后复查 exact 生命周期和租约有效性
→ project Host
→ commit
→ 返回成功
```

Model 从租约取得，不再额外 BindExecution。删除：

```text
AcceptedAdmission / LeasedAdmission / TargetedAdmission
BoundAdmission / ProjectedAdmission
ChatAdmissionBindingReceipt / HostModelProjectionReceipt
ChatAdmissionBindingKind（若迁移后确无独立业务用途）
Bind / Unbind ports
```

中间使用局部变量。成功结果不再返回生产调用者不用的四项回执；保留取消、superseded、queue full、already started/terminal 等真实处置差别。

日志可以记录经过的边界，但不为了日志维护一套“当前执行到第几步”的业务状态机。测试观察真实持久化、容量和 Host 效果，而不是迫使实现继续返回每一步的收据。

### 4.3 并发安排

同一 runtime、同一 exact key 的并发准入合并或等值幂等，保证一份 Accepted 和一个有效资源所有权。优先复用已有 exact-flight 和 queue 机制；若其作用域确实覆盖不了准入竞争，只允许在现有 runtime 内保留 `exact key → Task` 的在途表，任务结束即删除，不储存 agent/model，也不持久化。

不得在整个等待容量期间持有阻塞 cancel/terminal 的锁或串行队列。不得在现有同-key 串行操作内部递归等待同一个 flight。所有异步等待之后、Host 投影之前，必须确认执行没有被取消或取代。

`project → commit` 之间不添加新的异步间隙。commit 拒绝时必须阻断 provider；不能因为 Host 对象已经改过就假定准入成功。

### 4.4 单一失败结算路径

失败类型按处置区分，步骤名称只进入诊断。不要改成字符串判断，也不要给每个包装函数再造一层错误 DU。

沿用现有 `ExecutionFailure`、persistence commitment、typed disposition、capacity settlement。明确区分：未接受、Accepted 已提交、提交未知、已启动、终态已提交但资源结算未完。

对于已 Accepted 且尚未 ProviderStarted 的失败：由既有 policy 决定 disposition；确认对应 terminal 落盘；再精确结算已取得资源。没有取得租约就没有 release。terminal 写入未知或失败，不得在 finally 里盲目释放后返回成功。

拒绝一个错误请求，不等于有权结束同 session 的另一个合法执行。旧 fence、旧 physical 的补偿也不能触碰新租约。

### 测试与交付

补齐每个真实副作用边界的故障注入：append 未尝试/未知/成功后异常，acquire 等待期间取消，Host projection 失败，commit stale，terminal 写失败，exact release 重复与冲突。

正式回归走生产 Transaction 与 owner Surface；观察器不能替生产代码去重，测试不得复制一份准入/结算公式。

本批完成后，不再调用 `acceptExternalExecution / acceptPromptExecution / releaseAcceptedExecution`。

---

## P5：恢复、拓扑与资源清理归位，然后删旧模块

### 5.1 DevOps 固定目标先证明，再删缓存

保留道路既有持久化约束作为事实来源。核实从 `RoadDevOpsBound / BoundDevOpsModelTarget` 到合法 ModelRoutingTarget 的完整链，包括 reasoning、purpose、owner 与道路作用域。

运行测试：旧道路已经绑定目标；清空整个旧进程；改变当前 scheduler 偏好；新进程恢复并继续原 DevOps。应仍使用道路既有固定目标。相同 DevOps role 的 readonly-delegate 必须走自己的已授权用途，不能强套 owner 的目标。

目标缺字段、旧字符串无法无歧义恢复时，明确 blocked/manual，不从当前配置填空、不接受新模型后声称是恢复。需要新增数据格式或事实时单列迁移方案，不夹带进“语义等价重构”。

证明通过后，删除 `SessionExecutionBinding.persistentDevOpsModels` 及相应 bind/verify API。ModelRouting 所需的资源索引可以保留，但不是另一份可覆盖道路事实的配置。

### 5.2 清理路径改为 exact execution settlement

迁移 `PluginSessionScope.fs`、`Execution/Fission/OpenCode/Tool.fs`、`Sessions.fs` 等对 `SessionExecutionBinding.drop` 的调用。不能直接用 session-wide `ModelRouting.releaseExecution` 替代后声称精确。

会话取消/删除仍按已有 owner 流程枚举本作用域未结束的 exact execution，完成终态与资源结算。未准入预约、等待 demand 按其资源所有者的专用取消路径处理；不要把它们强行伪装成 ChatExecution terminal。

provider 的旧 terminal 只能结算它自己的 execution；工具单步结束只归还 step token，不删除整个 execution。诊断输出读取 owner 真相，不再以 `exactExecutionBindingCount` 证明资源持有。

### 5.3 拓扑恢复不再顺便恢复身份

从 `SessionBindingRecovery.fs` 删除安装 agent/parent 绑定回退的职责。handle、association、fission 的真实查询和恢复仍留在各自所有者；确有必要的 Host 辅助会话识别也保留在正式 adapter/生命周期记录中。

删除的是目标模块内这些身份/绑定集合，而不是全仓所有同名数据结构：

```text
parents / agents / internalRoots
acceptedPromptBindings / providerAttemptBindings
persistentDevOpsModels / durableChildEvidence
```

`hostAuxiliaryChildren` 的现有业务区分必须先迁往已有 Host 请求分类或生命周期上下文；不能先删后把“查不到 Accepted”当辅助请求。

### 5.4 完全删除旧入口

所有生产消费者迁完后删除 `SessionExecutionBinding.fs/.fsi`。`SessionBindingSurface.fs/.fsi` 中只为旧结构提供的测试出口一并删除；业务回归迁至新的生产语义出口，不为保住旧测试留兼容 facade。

`SessionBindingRecovery` 只剩拓扑职责时，移入既有加载/拓扑 wiring；是否删除整个文件取决于残留职责，不把有效 fission 恢复一起扔掉。

### 测试与交付

用两个独立进程和同一临时 durable workspace 验证重启，不能只调用 `drop()` 伪装进程死亡。至少覆盖 Blogger、普通 child、内部 lane、固定 DevOps。observer 两侧独立，不能共享去重集合。

Accepted 但未开始的旧 execution 是否可恢复，继续服从现有精确 Host evidence/recovery port；无该能力则 blocked/manual。不得自动重发 uncertain claim 或重放工具。

---

## P6：把内部投影变成互斥状态，并完成收尾

### 6.1 内部状态形状

修改 `Execution/Session/ChatExecution/Projection.fs/.fsi`、`Fold.fs/.fsi` 及其直接读取者。目标形状如下：

```fsharp
// 形状示意；构造权限收在 owner/fold，外部只查询或提交命令。
type PreStartOutcome =
    | Cancelled
    | Rejected
    | Failed

type ChatExecutionState =
    | Accepted of AcceptedChatExecutionEvidence
    | Started of ProviderStartedEvidence
    | EndedBeforeStart of AcceptedChatExecutionEvidence * PreStartOutcome
    | EndedAfterStart of ProviderStartedEvidence * ChatExecutionTerminalDisposition
```

不再同时保存 Lifecycle、ProviderStarted option、TerminalEvidence option。Key、accepted evidence 等从各分支所含证据计算，避免再存一份可以冲突的字段。

保留读取者真正需要的少量只读函数，不提供可写旧 record facade。不用类型变化为借口重写所有 unrelated execution 业务。

### 6.2 持久化兼容检查

本次原则上不改 `ChatExecutionFactCases` 的事件 wire 结构和版本，不改 PromptKey 派生，不重写历史日志。

但必须先检查 snapshot、incident envelope、replay codec 是否直接序列化当前内部 record。若是，维持既有外部编码并在唯一解码边界转换为新 DU；非法组合在此拒绝。需要升级版本时写明确、纯、可测试的升级函数，不可让内部 DU 的 `.tag/.fields` 意外成为新存储格式。

保留 Fable/JavaScript 的正式语义 Surface 和 `.fsi` 边界；不把消费者改成依赖 Fable 内部表示。

### 6.3 构建与规范元数据

检查并同步：

- `src/Wanxiangshu/compile-order.txt`
- 实际包含改动文件的 `Wanxiangshu.Owner.*.fsproj` 的 Compile 与 ProjectReference
- 对应 owner/locality 声明、APPLIES-TO、WHAT/HOW、测试映射
- JS Surface 导出、声明及所有实际消费者

`SessionExecutionBinding` 当前有独立 composition owner 项目；文件移走后检查引用方是否可删除对它的引用。不要为通过编译建立纯 domain → OpenCode 的反向依赖环，也不要给每个 helper 再建一个项目。

只修改真正涉及的声明，不批量生成全仓文件，不格式化无关源码。

---

## 5. 生产行为验收矩阵

以下用例追加到现有编号测试，不另建一套只测新框架的测试体系。具体 WHAT 锚点跟随实际被验证的条款。

| 场景 | 操作与观察 | 必须成立 | 主要归属 |
|---|---|---|---|
| 首次外部 managed 消息 | 无身份缓存，合法显式身份 | Accepted 先于 acquire/project；准确目标 | managed-chat [003/011] |
| 内部 Root | 无 session agent 缓存，由合法 IdentitySeed 发送 | 正常发出，Model=None，不伪造 Accepted | dispatch [010/012] |
| Continuation | 同 run，显式 agent 相同/不同 | 同身份延续；漂移拒绝，不借新物理消息换身份 | authority / dispatch |
| Session 复用 | 旧 run 已依法关闭，新 Root 合法选择另一 participant | 不因旧 session agent 冻结拒绝；旧事件仍不能影响新 run | authority [003/018] / managed-chat [001] |
| 只读查租约 | 已有/缺失/Pending/Released，重复查询 | scheduler、队列、fence、资源不变 | routing [009/012] |
| 同 key 并发准入 | 同物理消息同时投递两次 | 一份 Accepted，一个有效 lease；不重复 provider effect | managed-chat [004] / host [023] |
| A/B 交错 | A 未结束时 B 进入；随后 A 的 params/tool/terminal 到达 | A 不读写 B 的身份/目标/资源 | host [006] / routing [012] |
| 等待容量时取消 | Accepted 已落盘，demand 等待，然后取消 | 无 provider，exact terminal，等待与资源正确排空 | managed-chat [007/010] |
| 多步工具 | 同 physical 连续多个 ProviderRun | 每步 exact 交接，Accepted/ProviderStarted 不重复建立 | host [008] / routing [010] |
| Host 辅助请求 | 正式 HostInternal 证据 | 不造受管身份/租约；未知输入不冒充辅助请求 | host / authority |
| 发送未知 | transport 返回不确定或记录未确认 | 保留真实 pending/unknown，不自动重发 | dispatch [007/008] |
| 关键 crash cut | Accepted 前后、acquire 后、project/commit、start/terminal/release 边界 | 重启不虚构成功/持有资源；按真实证据恢复或 blocked | managed-chat [008/009/012] |
| Blogger 重启 | 同 durable workspace，新进程，新合法请求 | 不靠 agent 缓存回填也能恢复合法业务发送 | crash [020/021] |
| DevOps 重启 | 改 scheduler 偏好后恢复原道路 | 仍受原 durable 固定目标约束；readonly-delegate 不被误锁 | routing [019] |
| malformed wire | ID carrier 类型错、冲突、缺失；实际 variant 缺失 | 在真实边界拒绝，不用 expected 值补成一致 | dispatch [015] / host |
| terminal 重复/冲突 | 同 exact terminal 重复，或不同 disposition 到达 | 等值幂等；冲突不覆盖首个事实、不影响其他 execution | managed-chat [006] |
| unknown persistence | 写入未尝试/未知/已提交分别注入 | 三者不合并；未知不变成“安全重试” | managed-chat / failure policy |
| query/diagnostic | 连续读取诊断与恢复报告 | 不分配、不释放、不修复 owner 状态 | managed-chat [013/014] |
| 旧日志/快照 | 原字节在新版本读入，再恢复 | 编码兼容、历史不改写、非法组合明确拒绝 | durable-events / managed-chat |

测试不能因为从代码里删了一个 receipt，就顺便删掉证明持久化顺序和资源安全的断言。替换的是观测入口，不是业务要求。

---

## 6. 可执行的验证命令

以下是供实施者执行的命令，本次没有执行。以实际切换后的 HEAD 和依赖为准，不绕过 freshness 检查。

### 6.1 每个批次的准备

```bash
cd ~/Desktop/vibe/wanxiangshu
git status --short
git rev-parse --short HEAD
git diff --stat
```

### 6.2 构建

```bash
node scripts/build.mjs
```

禁止 `dotnet build`。不得修改 dist 来让测试通过。

### 6.3 单项回归

```bash
TESTS_MJS_FILES='requirements/execution-model-routing/tests/009.test.mjs' \
  node requirements/verification-system/tests/run.mjs
```

真实 Host 入口示例：

```bash
TESTS_MJS_FILES='requirements/host-boundary/tests/023.test.mjs' \
  node requirements/verification-system/tests/run.mjs
```

缺少 Host 二进制或不支持的实际版本时记录失败原因；不以跳过替代通过，不自动联网安装或升级 Host。

### 6.4 相关包回归（Bash）

```bash
shopt -s nullglob
files=(
  requirements/managed-chat-execution/tests/*.test.mjs
  requirements/execution-model-routing/tests/*.test.mjs
  requirements/dispatch-protocol/tests/*.test.mjs
  requirements/interaction-authority/tests/*.test.mjs
  requirements/host-boundary/tests/*.test.mjs
  requirements/crash-reconciliation/tests/*.test.mjs
  requirements/managed-session-lifecycle/tests/*.test.mjs
  requirements/session-ontology/tests/*.test.mjs
  requirements/provider-attempt-recovery/tests/*.test.mjs
)
((${#files[@]} > 0)) || { echo '没有找到测试文件' >&2; exit 1; }
TESTS_MJS_FILES="$(IFS=,; echo "${files[*]}")" \
  node requirements/verification-system/tests/run.mjs
```

若修改 Strength/readonly-delegate、provider projection、failure policy、snapshot 编码，必须加入对应 APPLIES-TO 所属包的用例，不能把上面的列表当完整依赖证明。

### 6.5 收尾门禁

```bash
npm run format-build-test
git diff --check
git status --short
```

仓库 `verify.mjs` 会主动清掉 `TESTS_MJS_FILES`，所以定向回归应直接调用正式 runner；不要以为把该变量加到 `format-build-test` 前就能只跑指定文件。正式 runner 检查 dist 新鲜度，不能使用 `--skip-staleness-check` 规避。

### 6.6 搜索残留

```bash
rg -n 'SessionExecutionBinding\.' src/Wanxiangshu
rg -n 'acceptedPromptBindings|providerAttemptBindings|persistentDevOpsModels|installDurableChildEvidence' src/Wanxiangshu
rg -n 'ChatAdmissionBindingReceipt|HostModelProjectionReceipt|AcceptedAdmission|LeasedAdmission|TargetedAdmission|BoundAdmission|ProjectedAdmission' src/Wanxiangshu
rg -n 'CurrentState\s*=' src/Wanxiangshu/OpenCode/Host/ChatAdmission src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs
```

搜索旧符号预期无生产命中；`rg` 的退出码 1 表示没有匹配，不是失败。文档中可保留历史说明，不能以历史文本命中要求改写日志。查到真实残留时说明用途，不能换个名字藏起来。

---

## 7. 提交、回退和最终交付

每批次按“失败测试 → 实现 → 对应测试 → 构建/门禁 → 审阅差异”闭环。开始实施前先确定相应授权；本稿不授权 commit/push/deploy。实施后仍遵守仓库已有分支和提交纪律，不 force push。

测试全绿不等于用户数据迁移安全。默认保持事件 wire 兼容；涉及新 snapshot/事件格式时另列正向解码和旧版本读取限制。只有确认新写数据仍可由旧版本读取，才能把回退代码当成可行回退；不能只说“git revert 就行”。

真正完成时交付：基线与最终 HEAD、被删状态/入口清单、迁移后的调用链、执行过的命令及结果、真实 Host 版本证据、旧日志/快照兼容结果、仍被阻塞的具体场景。不要只报减少了多少行。

### 完成定义

满足以下条件才算结束：

- 不再有独立 SessionExecutionBinding 身份/模型权威，且没有同义新 registry 替代它。
- managed send 不按会话拓扑补身份，不携带 model。
- params/transform/tool 通过真实 exact 证据关联；任何查询都不会隐式 acquire/rebind。
- 准入只有必要的持久化、资源和 Host 边界，没有中间步骤回执与调用者 CurrentState 副本。
- 重启不恢复旧进程租约，不因缓存缺失拒绝本可依据权威事实处理的新请求。
- 旧事件仍可读；真实身份/目标冲突、stale fence、unknown outcome 仍被正确阻断。
- 核心回归和真实 Host 契约通过，没有靠删除业务断言换绿灯。

**首个可交付批次是 P0 + P1：明确真实 Host 的 exact 关联，并交付经过生产测试证明不产生副作用的租约读取接口。随后迁消费者，最后才删旧写入端。**

---

# 重构架构设计

**User:** Anonymous  
**Created:** 9/29/2026 10:44:38  
**Updated:** 9/29/2026 11:15:26  
**Exported:** 9/29/2026 11:22:31  
**Link:** [https://chatgpt.com/c/6abb2615-7798-83e8-a5c1-25642d36ed89](https://chatgpt.com/c/6abb2615-7798-83e8-a5c1-25642d36ed89)  

## Response:
9/29/2026, 10:44:39 AM · gpt-6-pro

我先查清 PROMPT-006 的触发链、状态来源和已有修复，判断哪些复杂度能从数据模型与结构上消掉；这次只给重构方案，不改代码。

根因已收敛到 Host 重复保存并推断身份/执行状态；重写应分离容器、逻辑执行与物理 attempt，并让发送只接收已获准凭据。真实漂移仍在边界阻断，阶段状态则用联合类型直接排除非法组合。

> **梳理项目结构并重构PROMPT-006架构需求**
>
> 
>
> **检查会话执行绑定及恢复实现**
>
> 
>
> **评估调度架构并核查会话权限逻辑**
>
> 
>
> **审查聊天准入决策并核查路由权限证据**
>
> 
>
> **审查模型路由权威设计与崩溃协调测试**
>
> 
>
> **检查执行映射、事实投影缓存及聊天状态逻辑**
>
> 
>
> **梳理架构问题**
>
> 我正在判断是否需要外部核验；目前更关键的是本地验证。我的初步判断是，问题来自三种身份与生命周期被混在一起：会话、参与者角色，以及提示词分发和实际执行。
>
> **梳理了架构问题**
>
> 
>
> **厘清权威链路**
>
> 我正在把规范冲突收束为一条稳定链路：所有者意图先经调度，再接收入场并形成持久事实；随后获取执行租约、准备上下文、路由模型，最后记录启动与终止证据。身份应由租约绑定，不应复制临时参与者对象。
>
> **厘清权威链路**
>
> 
>
> **固定执行边界**
>
> 我正在明确一个关键区别：缓存缺失可以依据事实重读，能力或租约缺失则不能凭日志伪造。旧记录应兼容解码但不静默重建；迁移需拒绝冲突事实，并通过测试验证因果顺序、去重与过期状态。
>
> **固定了执行边界**
>
> 
>
> **整合实现方案**
>
> 我正在把结论压缩成可执行设计：问题不是缺少日志，而是权威身份没有贯穿主机边界。唯一入口应生成不可变执行上下文，下游只能消费它；我不会让缓存或临时对象重新决定身份。
>
> **整合实现方案**
>
> 
>
> **检查会话绑定并设计恢复与路由测试方案**
>
> 
>
> 思考了 9m 3s

我会重构，而且目标不是“让 PROMPT-006 少报一点”，而是：**让其中大部分错误状态根本不能被内部代码构造出来。**

你的判断有代码依据，不过需要说得更准确一些：**项目不是没有 SSOT，而是已经有权威记录，却又在 Host 层维护了一套可以独立变化的身份和执行状态。于是系统不断证明“两边仍然一致”。**

我的建议是：**重写 Host 执行绑定这一层，贯通现有的身份、执行和模型路由所有者；不重写整个系统，也不再新增一个万能状态管理器。**

## 一、目前的问题具体在哪里

以下判断基于当前 `master`（`567679845`）的静态审阅。代码路径以下省略 `src/Wanxiangshu/` 前缀。

### 1. 发送资格依赖“恢复器是否记得补齐这类会话”

`Sessions.routeSendOptions` 先判断是不是辅助会话、有没有受管父绑定，再选择 `prepareManagedPrompt` 或 `prepareUserFacingPrompt`；后两者其实都要通过 `tryAgent` 找到身份。

而 `tryAgent` 背后既有内存字典，也有 durable 回退；回退又分别认识 handle、Companion association，并把后者解释成 `"blogger"`。这使“某次发送是否合法”依赖于“所有会话形态是否都被恢复逻辑覆盖”。  
依据：`OpenCode/Host/Sessions.fs:173–183`、`SessionExecutionBinding.fs:250–280,1035–1047`、`SessionBindingRecovery.fs:47–70`。

**Blogger 的修复不是孤立的小遗漏，而是暴露了一种扩展方式：每增加一种生命周期，就可能要再教绑定恢复器一次。**

### 2. 执行身份要求精确，接口却仍然依赖 session 的“当前值”

规范明确规定 execution 的身份是：

```text
(SessionId, PhysicalUserMessageId)
```

但 `providerAttemptBindings` 按 session 保存一个当前绑定；`chat.params` 校验调用只传入 session、agent、model，没有传入本次观察到的 physical message ID。文件虽然定义了 physical ID 提取函数，却没有在这条校验链中使用。  
依据：`requirements/managed-chat-execution/WHAT.md` [001]；`SessionExecutionBinding.fs:57–77,329–351,932–984`；`ChatParamsHook.fs:81–116`。

这不等于已经证明每次都会串线，但它意味着：**精确性部分依赖调用时序和“当前值没有被换掉”，而不是接口本身保证。**

### 3. “观察”仍然会改写身份

`chat.params` 被定义为观察屏障，但调用路径里先执行 `observeUserFacingAgent`，再验证模型。观察 Host 字段和建立内部身份没有彻底分开。  
依据：`OpenCode/Host/ChatParamsHook.fs:180–187`。

### 4. 数据类型仍允许表达矛盾状态

`ChatExecutionState` 同时保存：

```text
Lifecycle
ProviderStarted option
TerminalEvidence option
```

因此类型上可以表达“阶段是 ProviderStarted，但没有 ProviderStarted 证据”。现在由 Fold 的运行时规则保证正常路径不产生这种组合，而不是由类型结构排除。  
依据：`Execution/Session/ChatExecution/Projection.fs:10–15`、`Fold.fs:56–112`。

**这些问题共同指向：同一个事实有多个可写表达，或者状态结构本身包含了无意义组合。**

## 二、我会采用的结构：一次确定，单向传递

核心流程改成：

```text
owner 签发的明确意图
        ↓
Dispatcher：记录 claim，发送；不选择模型
        ↓
真实物理消息被接收
        ↓
Admission：建立 durable Accepted
        ↓
ModelRouting：取得 exact execution 的目标和租约
        ↓
只读、不可自行构造的 ReadyExecution
        ↓
Host 投影与各 hook 消费同一执行上下文
        ↓
执行所有者记录 ProviderStarted / Terminal
```

这里的关键不是多加一个 `ReadyExecution` 名字，而是**不再允许下游重新拼装它的事实**。

### 身份、执行、资源仍然分别拥有，不合并成大对象

| 要回答的问题 | 唯一来源 | 作用域 |
|---|---|---|
| 谁以什么身份执行，依据哪次授权？ | participant identity owner 签发、authority 保管的 identity evidence | LogicalRun / AuthorityRoot |
| 哪条物理消息处于哪个执行阶段？ | `ChatExecution` durable facts 的折叠结果 | Session + PhysicalUserMessage |
| 本次使用哪个目标、持有什么容量能力？ | `ModelRouting` | Exact execution + fence；provider step 单独管理 |

这三个所有者项目已经有基础，不需要重新发明。现有 `AcceptedChatExecutionEvidence` 也已经通过 `IdentitySeed` 提供 participant 和 role。  
依据：`Execution/Session/ChatExecution/Facts.fs:10–39`；`requirements/dispatch-protocol/WHAT.md` [012]；`requirements/execution-model-routing/WHAT.md` [006/010/012]。

**SSOT 不意味着所有信息只能出现一次，而是同一个决定只能有一个所有者。** 不可变证据的传递、只读索引、Host wire 投影都可以存在；不能让它们独立决定身份或模型。

父子关系、Companion association、fission membership 则继续服务于各自的生命周期。**它们不应成为发送层猜测 participant 的通用入口。**

## 三、具体怎样改

### 1. 发送接口接收完整的发送意图，不再接收等待补齐的 options

现在的模式大致是：

```text
sessionId + Agent option + BindingIntent
    → 查 session 状态
    → 判断应该用哪个 agent
    → 检查是否一致
```

我会改为接收 owner 签发的封闭类型：例如“准备好的 Root 发送意图”或“绑定既有 run 的 Continuation”。

其中必须已经包含发送所需的 identity evidence 和精确作用域。Dispatcher 只负责验证该意图仍可使用、记录 claim、投影发送参数，不再从 `tryParent`、`tryAgent` 或 Host agent 字符串补身份。

这里要严格区分：**允许发送的意图不等于已经执行的事实。** 新 Root 尚未物理接收时，不能提前伪造 `Accepted` 或 Root acceptance。

这样，`prepareManagedPrompt`、`prepareUserFacingPrompt` 这两条按会话形态区分、最后却做相似身份检查的路径，就没有继续存在的必要。

### 2. 把执行阶段改成真正互斥的类型

示意如下，沿用已有证据类型，不另造一套事件协议：

```fsharp
type PreStartOutcome =
    | Cancelled
    | Rejected
    | Failed

type ExecutionState =
    | Accepted of AcceptedChatExecutionEvidence
    | Started of ProviderStartedEvidence
    | EndedBeforeStart of
        AcceptedChatExecutionEvidence * PreStartOutcome
    | EndedAfterStart of
        ProviderStartedEvidence * ChatExecutionTerminalDisposition
```

这样：

“Started 但没有 started evidence”“尚未启动就 Completed”“Terminal 但没有终态证据”都没有对应的内部表示。

状态构造和转移收在 owner 模块内；外部只能发命令、读取结果。日志解码、重复事实、冲突事实仍需校验，但业务代码不再承担维护这些字段组合的任务。

### 3. 用 exact admission handle 取代 Host 的多套绑定表

`ReadyExecution` 应是 Admission 成功后签发的进程内、不透明上下文，引用：

```text
已经确认的 Accepted evidence
对应的 exact lease capability
```

participant 从 evidence 读，模型从 lease 读。不要再分别维护可以写入的 `Agent`、`ExpectedModel`、“当前 session model”。

各 hook 通过公开物理证据关联到 **exact execution**，取得同一个上下文：

- `chat.params` 只检查 Host 实际观察是否匹配，不写身份、不重选模型。
- transform 管理相应 provider step 的容量，不重新确定 participant。
- tool / terminal 事件按 exact provider run、execution、fence 处理，不能拿 session 的最新绑定代替旧事件所属执行。

跨 hook 确实需要一个进程内索引时，它只保存 **exact key → admission handle**，不成为第二套身份或模型权威。

这里也不把多个 provider step 混成一次调用：同一 physical execution 可以包含多个 Host provider run，`ProviderRunIdentity` 仍只能在真实观察出现后建立，不能提前预测。现有规范已经要求这一点。  
依据：`requirements/managed-chat-execution/WHAT.md` [005]、`requirements/host-boundary/WHAT.md` [004]。

### 4. 将 wire 兼容性限制在一个边界

provider/model、variant 和身份字段的 wire 解析，集中到 Host adapter。域内使用明确类型，不到处 `Trim`、忽略大小写、接受 legacy alias。

但这不是“一律规范化后放行”：**哪些表示等价，必须由相应协议明确支持。** 真正不同的 reasoning、未知 participant、相互冲突的 carrier，仍然拒绝。

这比继续给 `sameModel`、`isManagedName`、恢复器分别加容错规则更容易控制。

## 四、重启恢复不会再是“把所有字典补回来”

新结构下，重启后主要做两件事：

**语义事实从现有 journal 重新折叠；进程资源按正常准入或明确恢复流程重新建立。**

不能混淆两种缺失：

> 身份缓存没有命中，可以重新读取权威事实。  
> 旧进程的 lease 已经不存在，不能靠读取事实就宣称仍持有容量。

因此，新物理消息可以使用恢复出的合法 authority 正常准入；旧执行能否继续，仍由 exact Host evidence 和已有 recovery policy 判断，不能因为“状态已重建”就自动重发。

DevOps 的固定目标也应只消费道路初始化持久化的目标约束，由模型路由所有者执行；Host 不再额外维护 `persistentDevOpsModels` 来重复判断。这个固定目标约束不能因简化而取消。  
依据：`requirements/managed-chat-execution/WHAT.md` [008/009/012]；`requirements/execution-model-routing/WHAT.md` [019]。

**所以，我不会承诺重构后永不报错。** 真正的身份冲突、过期 fence、发送结果未知、Host 缺少精确关联证据，仍必须阻断。消失的应是“事实明明存在，只因另一张表没同步好就报错”。

## 五、实施边界与验收

**建议状态：可以进入设计和纵向原型；本次不授权改代码或迁移数据。**

我会先用已有的 **Blogger 重启后再发送** 场景做一条完整纵向路径，贯通发送意图、admission、Host hook、终态，而不是先全仓搬文件。现有 `crash-reconciliation/020` 测试已经提供了该问题的回归材料，但相关用例主要验证恢复后 `tryParent/tryAgent` 的结果；新验收应验证对外执行行为，而不是继续要求旧缓存被填回去。  
依据：`requirements/crash-reconciliation/tests/020.test.mjs:237–395`。

验收重点只有四项：

1. **删除非权威身份缓存后，合法发送的判断不变。** Blogger、普通 child、内部 lane 都不需要 Host 绑定恢复器按种类补 agent。
2. **新旧物理消息交错时不串执行。** 旧 hook、旧 tool、旧终态不能读取或释放新执行的绑定和容量。
3. **每个关键持久化边界发生重启后，事实不丢、不虚构成功，也不盲目重发。**
4. **真实漂移仍然被挡住。** participant、model/reasoning、physical ID、fence 的冲突不能被“容错”吞掉。

实施前最重要的待验证项是：**真实 Host 的各 hook 是否都能通过公开证据关联到 exact execution。** 用 Host canary 验证；若有缺口，就明确修改集成契约，而不是再造一个 `currentSessionExecution` 猜测层。

旧日志保持不变，尽量只改变折叠后的内部表示；历史证据不足的情况留在明确的兼容解码或 blocked recovery 边界，不能按名称猜身份。

**最终应删除的是一套平行权威，而不仅是几条报错：发送层不再猜身份，Host 不再保存独立的身份和模型决定，恢复层不再负责让多张绑定表重新一致。** 这才是这一块遵守 SSOT 和 KISS 后应有的结果。

本次只进行了代码、规范和测试源码审阅，未修改文件，也未运行测试或复现全部历史事故。

## Response:
9/29/2026, 10:55:29 AM · gpt-6-pro

可以。上一版仍有不少只为“证明上一层做对了”而存在的交接层；我再沿实际调用链检查，能直接撤掉的就撤掉，只保留不可丢的语义边界。

> **精简执行契约**
>
> 我会保留最小状态流：接单→持久化→执行→发送→完成。把定时器、重试和日志下沉到运行时内部；外部只暴露重试入口、取消入口与最终状态。
>
> **精简了执行契约**
>
> 
>
> **压缩执行链路并核验调度声明**
>
> 
>
> **收束执行边界**
>
> 我会把“接单、落库、执行、发送、完成”收成一条主路径，但不合并调度、重试和容量控制。统一编排只保留一个入口，外部边界继续负责解码、持久化与结果投影；重试、取消和状态查询保持独立。
>
> **收束执行边界**
>
> 
>
> **保留必要凭证**
>
> 我会移除局部包装，但保留跨持久化边界所需的不可变接纳凭证。执行历史冻结接单时的证据，不重新读取当前状态；缓存只优化访问，失效不能伪造资源绑定。
>
> **压缩不可约步骤**
>
> 我会保留核验、落盘、租约、投影、提交这五个必要动作，但让它们成为一条顺序流程，而不是多个审批对象。失败统一归类，结算后再释放；并发仍由现有键、围栏和租约保证。
>
> **完成了不可约步骤压缩**
>
> 
>
> **审查插件互操作与HostSignalBootstrap冗余逻辑**
>
> 
>
> 思考了 4m 28s

能。**上一版仍然偏重“交接凭据”。再简化，就不应新增一个 `ReadyExecution` 管理层，而应直接撤掉独立的绑定层，复用已有的 Accepted 事实和模型租约。**

关键不是把七个步骤包装成一个函数，而是删掉其中几份状态，让相应的同步、校验、回滚和恢复一起消失。

## 一、先删没有独立用途的中间层

刚才复查了当前代码。`ChatAdmission/Transaction.fs` 里已经有这样一串中间对象：

```text
AcceptedAdmission
  → LeasedAdmission
    → TargetedAdmission
      → BoundAdmission
        → ProjectedAdmission
```

成功结果又带回 `witness、target、binding receipt、projection receipt` 四项。但生产调用方只匹配 `Settled _`，并不读取这些内容。多种阶段错误到了 `PluginHostInterop`，也被归并成相同的失败类别和结算状态。  
依据：`OpenCode/Host/ChatAdmission/Transaction.fs:44–51,104–134`；`HostSignalBootstrap.fs:637–650`；`PluginHostInterop.fs:246–267`。下文路径均省略 `src/Wanxiangshu/`。

这里可以直接减：

| 现在的结构 | 简化后的处理 |
|---|---|
| 五种准入中间对象 | 同一个函数里的局部变量 |
| Binding receipt、Projection receipt | 删除；调用成功返回即可 |
| 调用者传入 `CurrentState` | 删除；执行所有者按 exact key 自己读取 |
| 独立的 `Bind / Unbind` 步骤 | 随平行绑定表一起删除 |
| 逐步骤定义、逐层转译的错误 | 只保留会改变处置的区别；步骤名称留在诊断中 |

**类型应该区分真正不同的业务状态，不必记录函数执行到了第几行。**

`Accepted` 与 `Started` 值得分型，因为重启后的处理不同；`TargetedAdmission` 与 `ProjectedAdmission` 只是同一次函数调用中的进度，不必各设一种嵌套对象。

但 `Accepted` 已确认落盘的结果和租约能力应保留：它们证明的是持久化承诺、资源所有权，不是普通步骤回执。

## 二、准入收成一个直线函数，不再是一个小框架

我会把准入实现收回现有的执行所有者，不建立新的 coordinator、registry 或通用 transaction engine。

成功路径只剩：

```text
按 exact key 接受并落盘
    → 取得该执行的租约
    → 把租约目标投影给 Host
    → 确认准入完成
```

对应到代码，就是一个函数内顺序调用现有的持久化、模型路由和 Host adapter。中间不再有：

```text
查询 session 身份
建立另一份 agent/model binding
生成 binding receipt
生成 projection receipt
把这些凭据逐层装箱返回
```

**失败处理也收在这里。** 已接受但未启动 provider 的失败，交给既有 failure policy 决定处置，然后完成 exact terminal 持久化和 exact 资源结算。结算失败或结果未知必须保留，不能用一个 `finally release()` 假装收拾干净。

现有 `acquire / commit` 是否还能合并，需要检查容量取消和所有权转移的语义；这一点不靠改名消除。不过，即使保留两个调用，也不需要两套上层对象替它们作证。

还有一处值得直接去掉：当前调用者既提交 `Intent`，又提交自己读取的 `CurrentState`。我会让执行所有者自己按 key 读取状态，避免再维护“请求和随附状态是不是同一个执行、是不是已经过时”的约束。  
依据：`ChatAdmission/Transaction.fs:81–84`；`HostSignalBootstrap.fs:601–616,637–645`。

## 三、下游只认 exact key，不再保存一份“当前绑定”

这是比删样板更重要的一刀。

后续 Host hook 拿到真实的：

```text
(SessionId, PhysicalUserMessageId)
```

就分别读取：

```text
participant ← 该 key 的 Accepted evidence
model       ← 该 key 的 ModelRouting lease
```

**不再额外保存一份包含 agent、model、physical ID 的 ExpectedBinding。**

因此，上一版建议的 `ReadyExecution` 也不需要再成为注册、查找、恢复的对象。函数内部临时拿到 evidence 和 lease 就够了；确有跨回调需求时，也只传现有租约句柄或精确关联，不复制决定。

`chat.params` 只检查 Host 的实际参数；transform 只处理对应的 provider step；它们都不再修改身份或重新推导模型。

这也给 SSOT 一个很朴素的判据：

> 下游缺少信息时，读取拥有它的模块，而不是给自己补一个可写字段。

同一 key 的重复准入仍要幂等；等待容量期间发生取消或 supersede，恢复执行时仍要核对有效性。**消除平行状态，不等于取消并发控制。** 但这些控制留在执行和资源所有者内部，不散落在每个 hook。

## 四、发送不再经过“按会话种类补身份”

业务方发送消息时，已经知道是在发起新工作，还是延续某次工作。发送接口就接收这个意图，使用对应的权威身份。

不再走：

```text
这是 child 吗？
这是 internal root 吗？
是不是 user-facing？
内存里有没有 agent？
没有的话，能否从 handle 或 association 猜回来？
```

Root 与 Continuation 的授权差别保留，但只在意图接纳处处理一次。Dispatcher 负责 claim 和发送，不负责根据会话拓扑重建身份。

因此，`prepareManagedPrompt / prepareUserFacingPrompt` 这类分流，以及 `SessionBindingRecovery` 中**专为填补 agent/parent 绑定缓存而存在的部分**，可以退出发送链。不能把整个恢复模块连同真正的 handle、association、fission 恢复一起删掉。上一轮审阅已看到这些职责混在同一恢复入口里。  
依据：`OpenCode/Host/Sessions.fs` 的 `routeSendOptions`；`SessionBindingRecovery.fs` 的 `evidenceFor / install`。

这样，新加一种会话用途，不应再要求修改通用发送层和身份恢复层。

## 五、错误按“下一步怎么办”区分，不按“第几层出错”区分

当前很多错误名是在复述操作步骤：取得目标失败、目标转换失败、绑定失败、投影失败、提交失败。可以保留这些诊断位置，但不必把每个位置都变成一种贯穿多层的业务错误。

处理逻辑直接消费现有的 typed failure 和结算证据：

**明确拒绝、已被取消或取代、持久化或发送结果未知、真实不变量冲突**，这些区别不能丢，因为它们改变后续行动。两个失败若处置完全相同，只是发生位置不同，就不值得再造两个传播类型。

这不是改成 `Result<_, string>`。它是在保留语义的同时，删除重复翻译。

## 哪些边界不能压掉

**发送意图、物理接收、provider 启动不能合成一个“成功”。** 它们之间发生崩溃时，结果确实不同。Exact execution key、容量 fence，以及未知结果不得盲目重发的约束，也不能拿掉。现有 dispatch 与 managed-chat 规范已分别规定这些边界。  
依据：`requirements/dispatch-protocol/WHAT.md` [002–008]；`requirements/managed-chat-execution/WHAT.md` [003–009]。

所以，最终不是“一张万能表管全部”，而是：

**执行事实仍归执行所有者，资源仍归模型路由；两者之间不再夹一套 SessionExecutionBinding 来重新登记、互相证明。**

第一刀就应落在这里：**删除平行绑定状态及其 `Bind / Unbind` 生命周期，然后把准入中间对象压回局部变量。** 验收不仅看测试是否通过，还要看能否真正删掉对应的恢复分支、错误分支和维护它们的接口。只是把旧逻辑搬进一个新类，不算精简。

本轮仍是只读审阅，未修改文件或运行测试。

## Response:
9/29/2026, 11:01:08 AM · gpt-6-pro

我会把方案落到文件改动顺序、接口、测试和切换步骤，并逐项核对必须保留的约束。这次只给实施稿，不修改仓库。

先划清删除边界：provider-start 与工具交接要先迁走；`tryLease` 另拆只读查询，`acquire/commit` 保留。Host canary 作为切换门槛，并补交错消息、多步工具场景。

> **制定实施蓝图**
>
> 我正在先核对项目结构、约定、入口文件和现有测试，再把需求拆成可执行的代码改动、数据模型、状态流转、错误处理、兼容性与验证步骤。最终会明确文件路径、修改顺序和验收标准。
>
> **制定实施蓝图**
>
> 
>
> **制定Babysitter实现计划并审查提示发送与租约回调**
>
> 
>
> **审查模型路由、租约调度及验证依赖配置**
>
> 
>
> **审查聊天验证执行逻辑及主机边界测试**
>
> 
>
> **审查事件参数转换及套接字调度流程**
>
> 
>
> **审查并优化 DevOps 绑定、编译及模型路由逻辑**
>
> 
>
> **梳理实施方案**
>
> 我正在把需求拆成可直接执行的改动清单：先确认现有生命周期、存储与宿主边界，再定义最小接口、并发规则、错误语义、测试矩阵和迁移步骤。最终会让每项修改都对应明确文件、函数、调用顺序与验收条件。
>
> **梳理了实施方案**
>
> 
>
> **梳理迁移顺序**
>
> 我正在按“先加读能力、再迁移调用方、最后删除旧写入”的顺序组织实施，确保任何阶段都只有一个权威状态源，并保留可回滚的中间状态。
>
> **梳理迁移顺序**
>
> 
>
> **制定并检索守卫代码迁移及执行上下文方案**
>
> 
>
> **确定实施边界**
>
> 我会复用现有准入与队列能力；只有在它无法覆盖并发准入合并时，才在运行时内部增加短生命周期的同键任务合并表。它只合并同一次准入，不持久化业务状态，也不建立全局注册中心。
>
> **确定实施边界**
>
> 
>
> **检查只读容量快照与生命周期状态**
>
> 
>
> **起草执行绑定去层化实施方案**
>
>
> **起草执行绑定去层化实施方案**
>
> 
>
> **修订 P0-P6 实施计划与测试门禁**
>
>
> **修订实施计划并补充会话复用验收场景**
>
> 
>
> 思考了 13m 59s

按这个顺序施工：**先迁读取端，再删写入端，最后删除绑定层和样板。** 不从删除 `SessionExecutionBinding.fs` 开始，否则很容易又进入“哪里报错补哪里”的循环。

[完整实施稿（Markdown）](sandbox:/mnt/data/PROMPT-006-implementation-plan.md)

基线是审阅时的 `master：ab62b7d2f`。下面给出施工顺序、接口改法和验收要求；文件版还包含逐文件处理、故障注入矩阵、构建元数据与回退条件。**本次未修改仓库，也未运行构建、测试或真实 Host canary。**

以下源码路径省略 `src/Wanxiangshu/` 前缀。

# 一、先定完成后的样子

最终保留这几条实际业务边界：

```text
插件发送
已有 authority / IdentitySeed → durable claim → Host transport

物理消息准入
解码和身份判定 → durable Accepted → acquire → project → commit

后续 hook
exact execution key → Accepted evidence + 已提交的租约

执行结算
既有 execution owner + failure policy → durable terminal → exact 资源结算
```

不再有一套独立的 `SessionExecutionBinding`，重新登记 agent、model、physical message，再让各处反复证明这些副本一致。

这次同时固定三个范围决定：

**不新增 `ReadyExecution` 管理层、通用事务引擎或总协调器。** 已有证据与租约够用。

**保留 `acquire / commit`。** 容量层确实区分 Pending 和 Committed，不能把真实资源状态当样板删掉。删的是外面的中间对象和交接回执。依据：`OpenCode/Host/ModelCapacity/Surface.fs`。

**准入编排暂留 `OpenCode/Host/ChatAdmission/Transaction.fs`。** 不为了“归到执行所有者”而把 Host、ModelRouting 依赖倒灌进纯执行模块。语义归属和文件位置是两回事。

# 二、施工分七批，每批都要能构建、能验证

| 批次 | 交付物 | 前置条件 |
|---|---|---|
| P0 | 基线、规范调整、Host 精确关联验证 | 无 |
| P1 | 真正只读的 exact 租约查询 | P0 |
| P2 | params、transform、provider-start、tool 读取端迁移 | P1及对应 Host 契约通过 |
| P3 | 发送端直用已有 authority，删除补齐身份路径 | 调用点清点完成 |
| P4 | 准入直线化，删除 Bind/Unbind 和步骤回执 | P2、P3 |
| P5 | 恢复与清理归位，删除旧绑定模块 | P4及 DevOps 恢复证明通过 |
| P6 | 执行投影分型、兼容性检查、最终收尾 | P5 |

P2、P3 的调查与测试设计可以并行；重叠文件不要并行修改。**不允许把一批已知失败的新测试单独推入主分支，再让后续批次收拾。失败测试和对应实现同批交付。**

## P0：先把实际边界摸清楚

### 1. 建立基线

开始实施时重新检查 HEAD、工作区和 diff。当前方案基于指定提交，不意味着施工时仓库仍未变化。

先跑相关测试，记录原有失败的测试标题、命令、日志位置。后面要能区分“本次引入”与“原来就失败”。不能靠重跑、扩大超时、删除断言洗掉基线问题。

### 2. 先修改会把实现逼回旧结构的规范

局部调整现有 WHAT/HOW，不新建一套平行规范。

| 规范 | 要改的表述 |
|---|---|
| `managed-chat-execution` [003/009/011] | binding 不再意味着向 Host 第二次注册身份；进程内只保留必要资源 |
| `execution-model-routing` [009/011] | 后续 hook 使用只读查询；普通执行明确先 Accepted，后 scheduler/acquire |
| `dispatch-protocol` [010/012] | 发送身份直接来自已有证据；managed send 不接收 model |
| `crash-reconciliation` [020/021] | 从“必须回填绑定缓存”改成“没有该缓存仍能正确处理业务” |
| `host-boundary` 相关条款 | 明确各 hook 的 exact 关联，不允许 session-current 代替 |

**不能顺便删掉 Accepted 前置、fence、未知结果不重发等安全要求。** 修改的是实现约束，不是业务底线。

### 3. 先通过真实 Host 契约门槛

仓库已有 `requirements/host-boundary/tests/023.test.mjs`，检查 params 的物理消息 ID、assistant 的 parentID 和重复 messageID 投递。要复用并扩展它，而不是另起一个只会喂正确参数的模拟器。

必须验证：

- `chat.params` 的 message ID 是本次 physical user message，不是 assistant ID。
- transform 中的用户消息确实属于本次执行。
- 每个 ProviderRun 都能通过公开证据关联到 exact physical parent。
- **第二次、第三次工具后续 provider run** 也能关联，不能只验证第一轮。
- A、B 两条消息交错时，旧事件不会被解释成新执行。
- 准入被拒绝后，provider body 确实不会继续发生。

**这里查不到公开的 exact 证据，就暂停对应路径的切换。** 可以继续做只读查询和纯逻辑，但不能增加一个“当前 session 执行”回退来掩盖缺口。

## P1：增加真正只读的租约查询

### 修改文件

`ModelCapacity/Surface.fs/.fsi`、`ModelRouting.fs/.fsi`，以及对应生产测试 Surface。

拟定接口：

```fsharp
// 接口草图，不是已经实现的代码。
val internal tryReadExecution:
    key: ChatExecutionKey -> ExecutionAdmissionLease option
```

### 实现规则

**直接读容量所有者已有的 lifecycle 表，不新建查询字典。**

按 session 找到条目后，继续核对 physical ID 和阶段，只返回仍处于 Committed 的 exact lease。Pending、Released、其他 physical message 的租约，都不能作为本次可执行资源返回。

尤其不能调用现有 `tryLease`。它不仅查询，还可能接管预约、分配新租约、替换旧执行。依据：`OpenCode/Host/ModelRouting.fs:1176–1216`。

新查询不得：

```text
调用 scheduler
接管 reservation
发放 fence
commit / release
drain queue
回填身份
增加重复或冲突计数
```

participant、role 仍是租约资源身份的一部分，但只能与 Accepted evidence 核对，不能成为另一个独立选择身份的入口。purpose 也不能在查询时默认重算成 `Normal`。

### 验收

对命中、未命中、Pending、Released、错误 physical、旧执行被取代这几种情况，重复查询前后：

**scheduler 调用次数、队列、资源 ledger、fence、所有权均不改变。**

还要专门证明：存在 Strength reservation 时，查询不会把它升级成物理执行。

这一批先交付，不急于删旧 `tryLease`；旧调用迁完再删。

## P2：迁所有读取端，不再读绑定副本

### `chat.params`

修改 `ChatParamsHook.fs/.fsi` 和创建它的 wiring。给它已有执行投影的 exact 读取能力，不让它管理 journal 或恢复。

处理顺序固定为：

```text
解码真实 exact key 和实际模型
→ 读取该 key 的 canonical execution
→ 读取该 key 的 committed lease
→ 核对实际 agent、模型和 reasoning
→ 执行原有允许的参数投影
```

删除 `observeUserFacingAgent` 调用。

**没有查到 Accepted，不等于它是 HostInternal。** 受管输入缺证据要拒绝；只有正式的 HostInternal/辅助请求证据，才走其既有路径。

同样，实际 variant 缺失时，不能拿 expected variant 补上，否则检查变成了自己证明自己。

### transform

修改 `PluginTransforms.fs`。

保留 quiescence 和 provider step 的容量进入；删除清空、重建 `ExpectedBinding` 的操作。exact key 从本次真实消息取得，不从 session 最新状态取得。

重复 transform 与下一次真正的 provider step 必须区分。不能为了幂等，把同一 physical message 的所有后续 step 永久压掉。

本批不改容量借贷、lender 召回、排队公平性。

### AttemptPlan 与 ProviderStarted

旧绑定模块还包含：

```text
freezeProviderAttemptPlanForTransform
persistProviderStartedFromObservation
```

这两项不是缓存垃圾，必须保留。依据：`SessionExecutionBinding.fsi`。

把 Host-specific 部分迁回现有 ChatAdmission 集成目录。必要时增加一个只容纳这些函数的 `ProviderLifecycle.fs/.fsi`，**不能借搬家新增状态表或状态机**。纯决策继续调用已有 AttemptPlanner 和 execution owner。

保留原有语义：首次 plan 冻结；真实 run 出现后绑定；同一 physical execution 不重复建立 Accepted、ProviderStarted；terminal 后不能首次启动。

### 工具边界

当前 `ToolRegistry.fs` 只向绑定层传 session 和 ProviderRunId。必须改为取得这个 run 的 exact physical parent，再交给容量所有者。依据：`OpenCode/Tools/ToolRegistry.fs:361–368`。

优先在 Host adapter 构造工具上下文时完成关联。不能用：

```text
当前 session 的 binding
最后一条 user message
首个 provider run 的 plan
```

替代本次 run 的证据。

provider step 交接仍必须在业务工具体、权限检查以及可能等待后代 provider 的操作之前。旧工具事件不能结束新执行的 step；`tool-calls` 也不等于整个 execution 结束。

### 其他消费者

例如 `BloggerChronicleText.fs` 的 `currentProviderModel(session)`，改为接收材料所属 execution/run 的上下文，或读取对应真实记录。没有精确信息就明确缺失，不能显示另一次执行的“当前模型”。

**本批验收：生产读取端不再依赖 `providerAttemptBindings`、`acceptedPromptBindings`。** 暂时留下旧写入，不新增第二套写入。

## P3：发送端直用 authority

这一批不需要新增“发送凭证”。

现有 Dispatcher 已从 IdentitySeed/profile 取得 agent。但 `SendAgentOwnerRootWithTools` 仍接受可选 model，旧下游流程会影响它的最终发送参数。删除绑定层前必须先处理这个入口。依据：`Interaction/Dispatch/Send.fs:334–384,454–476`。

### 按此顺序改

**先去掉 managed send 的 model 参数。**

修改 `SendAgentOwnerRootCore`、`SendAgentOwnerRootWithTools` 及调用方：

```text
Strength/Replica/Runtime.fs
Execution/Delegation/SyncDelegate/Runtime.fs
相关 .fsi 和 JS Surface
```

managed options 固定 `Model=None`。底层通用 transport 的其他合法 model 用途不在本次一并删除。

Strength 的预约继续存在于 ModelRouting，由物理准入按原语义接管，不能通过发送字段走捷径。必须证明没有丢预约、没有二次调度、没有重复计数。

**再删除发送前补身份的链。**

Root 使用已有合法 IdentitySeed；Continuation 使用已有 active profile。移除：

```text
prepareManagedPrompt
prepareUserFacingPrompt
发送路径上的 tryParent / tryAgent 补齐
```

`Sessions` 只负责 transport 和真实生命周期，不负责选 participant。

`BindingIntent` 清点全部调用后，若确实只为旧补齐流程服务，就从 options 和 Surface 删除，不保留一个永远写 `Preserve` 的死参数。

### 外部用户输入另行处理

wire 中没提供 agent，就保留“没提供”。由 authority ingress 按现有合法规则解析 durable active/history，不能先从 session 缓存填一个字符串，再称它为 `ExplicitAgent`。

这既不能退化为“缺 agent 一律拒绝”，也不能退化为“默认 manager/engineer”。

### 验收

无 session agent 缓存的新 Root 正常发送；Continuation 保持原身份；错误显式身份仍被拒绝；managed send 始终 model-free。

还要验证：**旧 logical run 已依法关闭后，同一 session 可以接受合法的新身份，而不会被旧 session agent 冻结误拦。**

## P4：把准入收成一个直线函数

前两批迁完读取和发送后，才能删除绑定写入。

### 收紧入口

修改 `ChatAdmission/Intent.fs/.fsi`，让 Transaction 只接收真正的 managed intent，不再接收包含 HostInternal、Reject 等分支的整个 Decision，再在内部 `invalidArg`。

这里只需要一个排除非法输入的业务类型，不为每一步新建一种类型。

删除 `CurrentState` 输入。由执行所有者按本次 exact key 自己读取状态。当前代码由调用者读取状态再传入，产生了另一项需要维护的一致性约束。依据：`HostSignalBootstrap.fs:601–645`。

### 删除中间对象和回执

```text
AcceptedAdmission
LeasedAdmission
TargetedAdmission
BoundAdmission
ProjectedAdmission

ChatAdmissionBindingReceipt
HostModelProjectionReceipt

Bind / Unbind ports
```

其中真正需要的数据改为局部变量。保留已有的持久化确认和资源租约，不把它们混同于普通步骤回执。

生产调用者目前只匹配 `Settled _`，成功结果不必继续返回四项没人读取的包装。依据：`Transaction.fs:104–134`、`HostSignalBootstrap.fs:637–650`。

### 成功顺序

```text
读取 exact 状态，处理已开始、已结束及重复请求
→ durable accept
→ acquire
→ 等待返回后复查执行与租约仍有效
→ project Host
→ commit
→ 返回成功
```

不再有独立的 BindExecution。

`project → commit` 之间不添加异步间隙。commit 失败必须阻断 provider，不能因为 Host 对象已经改过就视为成功。

### 并发不能省掉

同一 runtime、同一 key 的并发准入必须合并或等值幂等，保证一份 Accepted 和一个有效资源所有权。

优先复用已有 exact-flight、queue。确实覆盖不了准入竞争时，只允许在现有 runtime 内保留 `key → Task` 的在途表，完成即删；不存 agent/model，不持久化，不另起 registry。

**不能在等待容量期间持有会阻塞 cancel/terminal 的锁或串行队列。** 所有等待之后、Host 投影之前，要重新确认没有被取消或取代。

### 失败结算只走一条路径

| 发生情况 | 处理要求 |
|---|---|
| 尚未 Accepted | 拒绝，不释放不存在的资源 |
| Accepted 已提交、尚未启动 | 交给现有 policy 决定 disposition，持久化 exact terminal，再结算已取得资源 |
| Accepted 提交结果未知 | 保留未知，不伪装成“没接受，可以重试” |
| terminal 写入未知或失败 | 不在 `finally` 中盲目释放后返回成功 |
| 旧 physical 或旧 fence 到达 | 不影响新执行的资源 |
| 同终态重复或冲突 | 等值幂等；冲突不覆盖首个事实 |

步骤名称进入诊断，处置差别保留为 typed failure。不要改成字符串判断，也不要继续为每个包装函数造一层错误类型。

**本批验收：生产代码不再调用 `acceptExternalExecution`、`acceptPromptExecution`、`releaseAcceptedExecution`。**

## P5：恢复和清理归位，最后删模块

### 先证明 DevOps 固定目标恢复

道路投影有 `BoundDevOpsModelTarget: string option`，但这不等于已经证明能完整恢复 provider/model/reasoning。依据：`Mission/Relay/Fold.fs`。

删除 `persistentDevOpsModels` 前必须通过这个测试：

```text
原道路已经固定目标
→ 旧进程完全退出
→ 改变当前 scheduler 偏好
→ 新进程加载同一 durable workspace
→ 恢复原 DevOps
```

仍须受原道路固定目标约束。相同 DevOps role 的 readonly-delegate 则按自己的合法用途处理，不能误套 owner 目标。

旧记录缺必要字段或不能无歧义恢复时，明确 blocked/manual。**不能从当前配置补一个模型，再声称恢复成功。** 需要数据格式变更，就单列迁移，不夹在语义等价重构里。

### 迁移 `drop` 的调用

检查 `PluginSessionScope.fs`、`Sessions.fs`、fission 等路径。

不能把：

```text
SessionExecutionBinding.drop session
```

机械替换成：

```text
ModelRouting.releaseExecution session
```

然后声称已经精确。

会话取消/删除仍通过已有 owner 枚举该作用域未结束的 exact execution，完成终态及资源结算。预约和未准入 demand 使用各自的资源取消路径，不伪装成 ChatExecution terminal。

### 删除缓存恢复，不删除业务恢复

从 `SessionBindingRecovery` 移除回填 agent/parent 绑定的职责；handle、association、fission 的真实生命周期与查询继续保留。

`hostAuxiliaryChildren` 的真实区分也必须先迁走，不能删除后把“查不到 Accepted”解释成辅助会话。

全部消费者迁完后，再删：

```text
SessionExecutionBinding.fs/.fsi
只服务于旧结构的 SessionBindingSurface API
旧绑定恢复入口
旧绑定计数诊断
```

业务测试迁到生产语义出口，不为旧测试保留 facade，也不能把 binding count 固定返回 1 来蒙混过关。

### 验收必须是真重启

使用两个独立进程、同一临时 durable workspace。不能只调用 `drop()` 模拟进程死亡。

至少覆盖 Blogger、普通 child、内部 lane、固定 DevOps。重启两侧 observer 独立，不能共享一个去重集合替实现吞掉重复调用。

旧执行能否继续，仍服从现有 Host evidence 和 recovery port；不因删了缓存就获得自动重发或重放工具的权力。

## P6：最后把投影改成互斥状态

修改 `ChatExecution/Projection.fs/.fsi`、`Fold.fs/.fsi` 及直接读取者。

目标形状如下，构造权限收在 owner 内：

```fsharp
type PreStartOutcome =
    | Cancelled
    | Rejected
    | Failed

type ChatExecutionState =
    | Accepted of AcceptedChatExecutionEvidence
    | Started of ProviderStartedEvidence
    | EndedBeforeStart of
        AcceptedChatExecutionEvidence * PreStartOutcome
    | EndedAfterStart of
        ProviderStartedEvidence * ChatExecutionTerminalDisposition
```

不再并列存储：

```text
Lifecycle
ProviderStarted option
TerminalEvidence option
```

Key 和相关 evidence 从分支内容计算。对外提供少量只读函数，不再保留可写旧 record facade。

### 这一步必须检查持久化边界

不能因为改的是“内部类型”，就假定持久化不受影响。

先查 snapshot、incident envelope、replay codec 是否直接序列化当前 record。若是，维持既有外部编码，在唯一解码边界转成新 DU；非法组合在此拒绝。

默认不改 `ChatExecutionFactCases` 事件编码、不改 PromptKey、不重写历史日志。不让 Fable 的 `.tag/.fields` 意外成为新协议。

同时同步真正相关的：

```text
compile-order.txt
Wanxiangshu.Owner.*.fsproj
.fsi 与 JS Surface
APPLIES-TO
WHAT / HOW / 测试映射
```

不为通过编译建立 domain → Host 的反向依赖，也不为每个 helper 新建一个项目。

# 三、最后验收不能只看“测试全绿”

这些行为必须由生产路径证明：

| 场景 | 必须成立 |
|---|---|
| 删除非权威身份缓存 | 原本合法的新请求仍可处理 |
| 重复 exact 查询 | 不调度、不分配、不回填、不改变资源 |
| 同 key 并发准入 | 一份 Accepted，一个有效租约 |
| A/B 消息交错 | A 的旧 hook、tool、terminal 不读写 B 的资源 |
| 等容量时取消 | 不调用 provider，exact 结算并排空 |
| 同 physical 多个 ProviderRun | 每步正确交接，不重复 Accepted/ProviderStarted |
| Blogger、DevOps 真重启 | 不靠旧进程绑定表恢复合法身份或资源 |
| 实际身份、模型、reasoning 漂移 | 仍然拒绝，不用 expected 值补齐观察 |
| 发送或持久化结果未知 | 不伪装成可安全重试 |
| 旧日志与快照 | 可读，历史不改写，非法记录明确拒绝 |

测试不再要求某张旧字典被填回去，但**不能因此删除原先证明持久化顺序、exact 身份和资源安全的断言**。

# 四、验证命令

每批次先构建，再用正式 runner 跑相关测试：

```bash
cd ~/Desktop/vibe/wanxiangshu

git status --short
git rev-parse --short HEAD

node scripts/build.mjs

TESTS_MJS_FILES='requirements/execution-model-routing/tests/009.test.mjs' \
  node requirements/verification-system/tests/run.mjs
```

真实 Host 契约入口：

```bash
TESTS_MJS_FILES='requirements/host-boundary/tests/023.test.mjs' \
  node requirements/verification-system/tests/run.mjs
```

收尾：

```bash
npm run format-build-test
git diff --check
git status --short
```

禁止 `dotnet build`、手改 dist、跳过 freshness 检查。`verify.mjs` 会清掉 `TESTS_MJS_FILES`，所以定向回归要直接调用正式 runner；不能以为把变量加在 `format-build-test` 前面就能缩小范围。依据：`scripts/verify.mjs`、`requirements/verification-system/tests/run.mjs`。

最后搜索旧模块、旧绑定表、旧回执和 `CurrentState` 输入的生产残留。**换名字藏起来不算删除。**

---

**第一个交付批次就定为 P0＋P1：证明真实 Host 的 exact 关联，交付一个通过生产测试证明无副作用的租约读取接口。**

接着迁消费者，再撤销绑定写入。这样每一刀都有明确对象和验收结果，不会把一次去层化重构做成另一轮反复修补。



---
Powered by [ChatGPT Exporter](https://www.chatgptexporter.com)