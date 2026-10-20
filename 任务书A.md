# 任务书A · 委派接口迁移：this.xxx + reportMapping

（本任务书由 DevOps 会话于 2026-10-20 整理，作为接棒施工的唯一交接文档。基准提交 HEAD `60812f575`，全部改动在 master 工作树内、尚未提交。）

## 一、目标接口约定（用户拍板的语义，不得偏移）

1. 顶层上下文整理（todowrite-for-* 工具参数）：扁平的 `prefixUntil: number` + `thenMap: record<number, string|null>` 两个顶层参数；**不再接受 `view` 包装对象**（已实现并运行时验证）。
2. 委派成员全部走编程面 `this.xxx`，**不保留独立 Host 工具注册**，每个必须携带 `reportMapping`（形状 = thenMap：`Record<string, null|string>`，无 prefixUntil）：
   - `this.fork(payload, reportMapping)`
   - `this.resume(payload, reportMapping)`
   - `this.commission(payload, reportMapping)`
   - `this.devops(intent, reportMapping)`
   - `this.advisor(intent, reportMapping)`（advisor 已是成员，需把"缺 view 静默按空"改为强制）
   - `this.deliver(reportMapping)`（已是 view-only；参数从完整 view 声明降为 reportMapping，宿主按 `{prefixUntil:0, thenMap:reportMapping}` 解释，语义不变）
   - `this.dag(spec, reportMapping)`：**一份 reportMapping 发给所有节点的 subagent**，节点不得覆盖。
3. `this.fission(payload)` **不带 reportMapping**：同身份换执行道，无条件继承本对话衣钵——上下文来源是顶层整理后的当前会话，而不是旧的交付记录。
4. `join`/`horizon` 是拉取动作，不是委派，不强加 reportMapping。
5. 代码尽量 DRY：`reportMapping` 的校验/区间投影/材料渲染与顶层 view、deliver、advisor 共享同一套纯函数。

## 二、已落盘的改动（工作树，未提交）

- `Anchor/AnchorSurface.fs`/`.fsi`：新增 `parseReportMapping`（包装为 prefixUntil=0 声明复用 parseDeclaration）与 `reportMaterial`（只读投影，**不消费 pending view、不写永久压缩**）。
- `Js/DelegationBridge.fs`/`.fsi`：`JsDelegationHost` 的 `Fork` 字段已改为 `Delegate: string -> obj -> obj -> Task<obj>`（name, payload, reportMapping）。⚠️ **ToolRegistry 尚未适配，当前源码编译必失败**——这是接棒后第一个要修的不一致点。
- `Js/ToolsBindings.fs`：`makeDelegateMember name` 统一生成 `fork`/`resume`/`commission`/`devops` 成员（payload, reportMapping），缺省/非法 reportMapping 走 `ViewDeclarationInvalid` 数据信封拒绝。
- `Js/Capability.fs`/`.fsi`：新增 `Resume`/`Commission`/`DevOps` 能力及 fragment（`ofToolPermission` 投影 Resume、DevOpsConsultation）。
- `Js/Surface.fs`：`tryProject` 对 Orchestrator 角色把 `Fork` 能力改投 `Commission`（成员名 commission）。
- `Js/GeneratorSurface.fs`：capabilityLabel 增加 Resume/Commission/DevOps。
- `Js/OpenCode/ToolHost.fs`：顶层 `view` 参数退役 → `prefixUntil`+`thenMap`；残留 `view` 参数直接拒绝；无参数时跳过 setView。**已运行时验证**（重启后宿主报错正是新文案）。
- `Execution/Fission/OpenCode/Tool.fs`：`ownerViewPort` 改为读 `AnchorLedger.context`（继承当前对话上下文）。⚠️ 需编译验证。

## 三、剩余施工（按顺序）

### 1. ToolRegistry.fs（核心收口，修编译）
- `generatedJsSpecFor` 的 host 闭包仍是旧 `Fork = fun payload ->`：替换为 `Delegate = fun name payload mapping ->`，按 name 分派：
  - `fork`/`commission` → `ForkTool.executeReceipt`（payload raw 注入 `commissioner_record` = reportMaterial 渲染文本）；
  - `resume` → 新增的 `ForkTool.executeResumeReceipt`（见下）；
  - `devops` → 同步调查：`devopsDelegateOf` + `PlanTools.executeAsk`（intent 文本 = 原 intent + 报告材料，见"待决"§7.1）；
  - 未知 name → `unknown-delegate` 拒绝。
- `devopsDelegateOf`/`askGitCommonDir`（现定义于 generatedJsSpecFor 之后）需要**上移**到其之前；`askWithLifecycle`/`renderAskOutcome` 随 devopsTool 退役一并清理。
- `baseSpecs`：移除 `ForkTool.managerSpec`/`resumeSpec`/`orchestratorSpec`/`devopsTool` 四个 yield；`devopsTool` 定义整体删除。
- `staticAdmissions`：移除 `fork`/`resume`/`commission`/`devops` 四项。
- `advisorDeclarationOf`：删除 `isNull → empty` 宽容，统一 `AnchorSurface.parseReportMapping`。

### 2. ForkTool.fs（Execution/Delegation/Fork/OpenCode/Tool.fs）
- `Request` 增加 `CommissionerRecord: string option`；`decode` 读 `args.OptionalText "commissioner_record"`（Trim，空→None）。
- `prepareForkPromptWithRecord` 改为直接取 `request.CommissionerRecord`（relayDocument 已有该槽位，方向：父→子背景数据）；三处 `request None attachment` 调用同步调整。
- 新增 `executeResumeReceipt`：镜像 `executeManagerResume` 的守卫链（name/charge 必填、calling-not-allowed、self-attach、`boundDevOpsDispatchRefusal`、`bindWorkDevOpsIfNeeded`），复用 `resolveAttachment`/`recordFissionAffinity`/`runManagerReuse`（空闲）或 `ActiveGuidanceProfile`+`AppendGuidance`（忙碌）；`ForkResult`→`ForkReceipt` 映射（DispatchUncertain→`dispatch-uncertain`）。
- `finishCommissionNewReceipt`：`CommissionerRecord` 存在时拼进 prompt（`charge + "\n\n" + record`），缺省保持现行为不破 wire 测试。
- 公开成员加入对应 .fsi/.fsi（若 ToolSurface 暴露面有类型约束）。

### 3. RuntimeSurface.fs/.fsi
- `installDelegationHost` 参数 `fork` → `delegate`（三参回调 name,payload,mapping）。
- `capabilityOfLabel` 增加 "Resume"/"Commission"/"DevOps"。

### 4. Description.fs（伪代码与运行时基类同步）
- runtimeBaseClassCore 的 fork 方法改双参；新增 `resume`/`commission`/`devops` 运行时方法（各自 capability 投影）；`dag(spec, reportMapping)` 内部 `this.fork({...}, reportMapping)`；`deliver(reportMapping)` 抛出 `err.__jsDelivered={view:{prefixUntil:0, thenMap:reportMapping}}`；`advisor(intent, reportMapping)`。
- publicBaseClass 增加 `{{baseClassResume}}`/`{{baseClassCommission}}`/`{{baseClassDevOps}}` 插槽（仅 Fork/Resume/Commission/DevOps capability 时注入；fork 插槽文件同时承载 dag）。

### 5. resources（双语成对，parity gate 会查）
- 改：`base-class/en+zh`（插槽）、`base-class-fork`（fork/dag 新签名）、`base-class-advisor`、`base-class-lifecycle`（deliver 新签名）、`deliver`、`delegation-rules`。
- 新建：`base-class-resume`、`base-class-commission`、`base-class-devops`（各 en+zh）。
- `js-plan` 资源：devops 改为成员调用 + reportMapping；整理参数改述扁平形式。

### 6. 测试
- `repository-programming/056`：hook 改 delegate 三参；`this.fork(...)` 全部补 reportMapping；非法/缺省 mapping → `VIEW_DECLARATION_INVALID` 数据信封。
- `advisor/006`：installDelegationHost 参数 + `this.advisor(intent, reportMapping)`；缺 reportMapping 拒绝。
- `capability-enforcement/025`：Manager capabilities 断言改为 `[Advisor,Fork,Glob,Grep,Horizon,Join,Read,Resume]`；api 绑定键断言补 `resume`。
- `delegation/019/024/026/027` wire 测试保持通过（spec 构造函数仍存在，只是不再注册）。
- 可选回归：`ToolHost` 顶层扁平整理参数 + `view` 拒绝；`reportMaterial` 纯函数（只投影不消费）；`fission` 上下文继承（无 reportMapping、读当前上下文而非交付记录）。

### 7. WHAT 文档
- `requirements/delegation/WHAT.md` [024]/[021]：父→子上下文 = 委派必传 `reportMapping`，宿主投影为 `commissioner_record`；退役"charge 里手工拼接上下文"的写法表述。
- `requirements/repository-programming/WHAT.md` [053]/[054]/成员清单：顶层扁平参数；委派成员 reportMapping；deliver(reportMapping)。
- `requirements/advisor/WHAT.md` [002]/[003]：advisor 收 `reportMapping`（等价 prefixUntil=0 声明），缺省拒绝。
- `requirements/context-compression/WHAT.md` [031] 保持为唯一规则源（引用，不复制）。

## 四、待决（需用户/Manager 拍板）

1. **devops 报告文本的形态**：当前设计把 reportMapping 材料追加到 intent 文本（devops 走 SyncDelegate，没有 commissioner_record 槽位）。是否接受，或给 SyncDelegate 加 typed 背景字段？
2. **host fork/resume/commission 彻底删除 vs 保留注册但拒绝**：本任务书按"移除注册"施工（delegation [020] 语义不因工具名变化）。

## 五、执行纪律提醒

- 构建只用 `node scripts/build.mjs`/`npm run check`，严禁 `dotnet build`。
- 提交前 `git status` 区分本任务改动与既有未提交改动（本任务书列出的都是本任务已做的）；自动提交推 master。
- 改任何文件前先 cat/where，工作树里还有并行痕迹。
- park 现场若在编程面产生，必须当场 unpark/abandon 闭环再交付（本次事故教训）。
- **关于 prefixUntil 的方向（纠正 DevOps 会话自己脑补的一条假规则）**：契约里从来没有「prefixUntil 只能提高不能降低」这回事，那条限制是我 thinking 里臆造的，不是规则。事实是两个方向都合法，各有正当用途。prefixUntil 是缓存前缀的保护下界：编号小于它的锚点进入廉价前缀、不再逐轮原价重算；大于等于它的锚点进入可替换区、每轮按原价计费直到你为它写摘要。由此——① 想把一段已经稳定、却仍在逐轮重算的旧内容移出原价：提高 prefixUntil，把它罩进前缀（注意 provider 要等这段字节重新缓存落定，通常下一轮起才见省）。② 想把一段确认作废的内容永久折叠成摘要：前提是该锚点处于可替换区；若它当前小于 prefixUntil、被保护着、本轮 thenMap 会静默忽略它，就得先降低 prefixUntil 让它重获可替换，再对该锚写摘要。摘要替换一旦提交即永久生效、不可逆；降低 prefixUntil 也不会恢复任何此前已压掉的原文——契约那句「提高边界不会恢复此前压缩掉的原文」只约束提高方向，与降低无关。一句话：升=省钱保护，降=解锁折叠，两向皆可，按当下目的选，别把它当单向阀。


## 六、当前事实快照

- HEAD `60812f575`，分支 master，工作树含上述 32 个文件改动 + 2 个新增资源目录（`base-class-anchors/`、`review-only/` 属既有未提交，非本任务产物）。
- `DelegationBridge`（新）与 `ToolRegistry`（旧）不一致 → **编译失败状态**，接棒先修。
- 顶层扁平参数 + view 拒绝已在重启后的运行时生效（工具报错文案为证）。
- 尚未运行 build、format、测试。


---

# 任务书B · Manager 程序入口常开 + this.reviewOnly 命名空间

（本节由另一个 DevOps 会话于 2026-10-20 追加。与任务A 在同一工作树并行施工，改动相互交叠；本节只交代任务B 的边界与剩余工作。基准提交同为 HEAD `60812f575`，改动未提交。）

## 一、目标语义（用户拍板，不得偏移）

1. `todowrite-for-manager` 程序入口对已确立身份的 Manager **总是能用**，不再因评审接纳/末任/清理窗口整票拒绝；身份、取消、等待、存储等独立边界照旧。
2. 仓库访问仅经 `this.reviewOnly.cat/stat/ls/rg`，**无顶层别名**；Host 在每次访问执行前核验当前任期事实（活跃任期 ∧ 评审未接纳 ∧ 无确认 ∧ 无清理阻塞 ∧ 未冻结）。末任不例外。
3. 拒绝用稳定机器码 `REVIEW_ONLY`（`JsFailure.ReviewOnly`），发生在路径校验之前：不读目标文件、不启动 rg。
4. `where`/`whereAll` 只处理已有文本，保持顶层；委托、生命周期成员各守自己的权限。
5. 保存对象/保存方法/原始 `_api` 绑定、挂起恢复（unpark）后重新访问，都必须重查当前窗口，不得复用旧授权。
6. 其他角色不受影响：共用底层文件实现，无 Manager 专属文件引擎；生成描述、示例、成员目录与真实 API 同用 reviewOnly 命名空间，评审前后同一份工具定义。
7. 前置已提交：`45bf5b74a` 删除了 `contract: do-not-use-except-for-review` 保证书参数（已验证并推送）。

## 二、已落盘改动（工作树，未提交；与任务A 改动混在同一批文件里）

- `Js/Failure.fs`/`.fsi`：`JsFailure.ReviewOnly`（code `REVIEW_ONLY`，reason 双语由资源承担）。
- `Js/ToolsBindings.fs`：`module JsReviewOnly.scopeReviewOnly`（对 cat/stat/ls/rg 逐次 guard → check 失败返回 `REVIEW_ONLY` 数据信封；删除 api 顶层四成员；挂 `api.reviewOnly`）。另补 `makeCommissionMember`（走 `delegation.Fork` 通道，给任务A 的 Commission 能力补四层同构，见 §四冲突）。
- `Js/Description.fs`：`publicReviewClass`/`publicBaseClassForRole`（Manager 生成 `class ReviewOnly {…}` + `reviewOnly = new ReviewOnly()`，顶层 cat/ls/rg 插槽清空）；`runtimeFileMethods`/`runtimeBaseClassForRole`（Manager 运行时基类把四个文件方法移入 `ReviewOnly` 类，顶层无别名）；`reviewCalls`（示例与 rules 里 `this.cat(` → `this.reviewOnly.cat(`）；`exampleSourceForRole`。另补 commission 运行时基类方法。
- `Js/Surface.fs`：`membersForRole`（Manager 的 cat/stat/ls/rg 成员名改 `reviewOnly.*`，memberBinding 随之）。
- `Js/OpenCode/ToolWorkflow.fs`：`bindingsApiFactory(reviewCheck)`、`runProgramWithPark(reviewCheck)`、`executeWithReviewCheck`、`execute` 包装（None → 无检查）。
- `Js/OpenCode/ToolHost.fs`：`createCore(reviewCheck)`、`createWithReviewCheck`；Manager surface 未配 check 时默认 `fun _ -> false`（fail closed）。⚠️ 注意：任务A 已把此文件顶层参数改为 prefixUntil+thenMap，两处改动同文件共存。
- `OpenCode/Tools/ToolRegistry.fs`：`reviewAllowed`（读 runtime.ManagerCapabilityFactsFor + IsRetirementFrozen）；`createSpec`（Manager 用 createWithReviewCheck，其余用 create）；`executeAdmittedRole` 对 `todowrite-for-manager` 跳过 `managerFactsStage`（入口常开）。
- `OpenCode/Plugin/PluginHooks.fs`：删除 `assertReviewPermitted` 整票拒绝；`checkManagerReviewPermissions` 只保留 blockedNative（Manager 借 todowrite-for-engineer/devops 绕路仍拒）。
- 资源：`tool/js-program/base-class-read/` 拆分（cat/stat 留此文件，whereAll/where 移入新建 `base-class-anchors/`，双语）；新建 `tool/js-program/review-only/`（双语规则）；`manager-assess`/`manager-work`/`manager-finish` 双语提示词改为 reviewOnly 表述；`base-class` 头注释更新。
- WHAT：`requirements/capability-enforcement/WHAT.md` [025] 重写为"程序入口常开、仓库访问仅限 Review"；`requirements/repository-programming/WHAT.md` Manager 段落同步。
- 测试：`capability-enforcement/tests/025.test.mjs` 重写（P11 插件重开入口常开+读取关闭；P13 逐操作接纳前可用/接纳后拒绝+顶层旧入口 undefined+scope 探针；P14 保存对象/绑定方法/原始 _api 绑定 + park 恢复后重查；P15 末任无例外+路径校验前拒绝；身份与跨角色独立；零物理访问+不启动 rg；in-flight rg 接纳后可完成、新调用拒绝）；`support/manager-programming.mjs` J01/J02/J04/J11 断言与命名更新（J01 capabilities 含 Resume，按任务A 的能力扩展）；`repository-programming/tests/001/002` 按 Orchestrator commission 能力校准。

## 三、已验证（当时代码状态；后被并行改动覆盖，不构成当前状态证据）

- 025 套件 32/32 全绿；`WXS_TIER_INTEGRATION=1` 下 host-boundary/032 canary 18/18；repository-programming 全套 300 tests 0 fail；provider-language + office-capability 197 tests 0 fail；控制金字塔 gate 对本任务触碰文件零新增违规（HEAD 对照脚本核验）。
- 以上绿轮之后，任务A 的并行改动进入同一批文件，多轮相互覆盖造成拼接伤（详见 §四），最终构建被中止，当前编译状态**未知**。

## 四、与任务A 的交叠与已知风险

1. **DelegationBridge 形态矛盾**：任务书A §二说 `Fork` 已改 `Delegate`（三参）；本会话最后一次 cat 时该文件是 **Fork 形态（无 Delegate 字段）**。本会话已把 ToolsBindings/ToolRegistry 里自己引入的 Delegate 引用改回 Fork 求一致。接棒第一步：cat `Js/DelegationBridge.fs` 确认当前形态，再决定按任务书A §三.1 收口还是维持 Fork。
2. **本会话给 Commission 补的基类方法/绑定走 Fork 通道**（Description.fs commission 方法、ToolsBindings makeCommissionMember），与任务A 的 Delegate 分派设计冲突；按任务A 收口时合并或替换，四层同构断言（002.test）当前按 commission 成员名校准。
3. **拼接伤修复史（均已修，未过最终构建）**：ToolRegistry `generatedJsSpecFor` 曾出现重复 task 块与 "surfaceeate" 残骸（已整体重写该函数）；ToolWorkflow `abandonSite` 缩进破坏（已修）；Description.fs commission 拼接语法错（已修一轮）。最后一次 `node scripts/build.mjs && node --test 025/001/002/041` 被中止，结果未知。
4. **测试期望已按任务A 能力扩展校准**：Manager surface 含 Resume 成员、Orchestrator 用 commission 替代 fork/dag（001/002/manager-programming 三处）。若任务A 回退能力扩展，这些断言要跟着回退。

## 五、剩余施工（任务B 视角，按顺序）

1. `node scripts/build.mjs` 确认编译；若 Description/ToolRegistry/ToolWorkflow 仍有拼接残骸，与 HEAD 对照修复（本会话的修复未过最终构建）。
2. `node --test requirements/capability-enforcement/tests/025.test.mjs requirements/repository-programming/tests/001/002/041` + manager-programming 全绿；`WXS_TIER_INTEGRATION=1 node --test requirements/host-boundary/tests/032.test.mjs` canary 全绿。
3. 与任务A §三.1 的 host 闭包重写合并：保留 reviewCheck 管道（ToolHost.createWithReviewCheck → ToolWorkflow.executeWithReviewCheck → JsReviewOnly.scopeReviewOnly），Manager facts 读取在 ToolRegistry.reviewAllowed。
4. `dotnet tool run fantomas` 格式化本任务触碰的 .fs/.fsi（Description、Surface、ToolsBindings、Failure、ToolWorkflow、ToolHost、ToolRegistry、PluginHooks）。
5. 提交：工作树混有任务A/B 两批改动 + 既有未提交（base-class-anchors/、review-only/ 属本任务产物）；按文件分批或与 Manager 商定一次性提交，推 master。

## 六、教训（本会话补充）

- 并行会话在同一工作树施工时，每次写前必须重新 cat/where；本会话多次 FILE_CHANGED 与拼接伤都源于对陈旧快照续写。
- park 现场必须当场 unpark/abandon 闭环（park-1/park-2 均已闭环），否则交付被泄漏兜底门禁拦截。
- 验证结论必须绑定代码状态：并行改动落地后，先前绿轮不再作数，最终构建+测试必须重跑。

---

# 八、接棒状态与剩余任务（2026-10-20 停电前盘点）

本节是**续接入口**。上文一到七节是当初的施工书；本节记录**已落地的部分**、**当前红/未验的部分**、**明确未做完的剩余任务**，以及**续接命令**。

## 八·一 基准与工作树现状

- 基准提交（原任务书所写）：`60812f575`。**当前 HEAD**：`df7846ab8 refactor: move delegation onto the programming surface and retire the devops tool`（由飞行中的一个 agent 在中途提交并推送，含委派迁移主体）。
- HEAD 之后**未提交**：受跟踪改动 **104** 个文件（`src/` 49、`requirements/` 35、`resources/` 20），新增未跟踪 **8** 个文件（见八·四）。
- **工作树里有第二个会话在同时施工**（做 `task.md` 的 deliver 语义）。它的产物 `debug/`（会话转储）与 `task.md`（它的任务书）**不属于本任务**，提交时**不要**带上。它在 19:41 留下过 `Description.fs` 的半成品（`deriveNameFromCharge` 重复定义 + `runtimeFileMethods` 的 `namespace` 保留字参数），本轮已修好：`runtimeFileMethods (capabilities) (memberNamespace) (guardOf)`，普通面走 `this._api` 无守卫，ReviewOnly 面走 `this._api.reviewOnly` + `REVIEW_ONLY` 守卫。

## 八·二 已落地（按层）

### 委派接口迁移（任务书A §一—§三）

| 层 | 落点 | 状态 |
|---|---|---|
| 委派入口 | `Js/DelegationBridge.fs`/`.fsi`：`Fork: obj -> Task<obj>` → `Delegate: string -> obj -> obj -> Task<obj>`（name, payload, reportMapping） | ✅ |
| 成员工厂 | `Js/ToolsBindings.fs`：`makeDelegateMember` 统一生成 `fork`/`resume`/`commission`/`devops`（各带 reportMapping）；`advisor` 强制 reportMapping | ✅ |
| 宿主闭包 | `OpenCode/Tools/ToolRegistry.fs`：`Delegate` 按 name 分派（fork/commission → `ForkTool.executeReceipt`；resume → `executeResumeReceipt`；devops → `devopsDelegateOf` + `PlanTools.executeAsk`；未知 → `unknown-delegate`）；报告材料来自 `AnchorSurface.reportMaterial` | ✅ |
| 退役 host 注册 | `staticAdmissions` 与 `baseSpecs` 不再含 fork/resume/commission/devops；`devopsTool` 整体删除 | ✅ |
| 运行时基类 | `Js/Description.fs`：`fork/resume/commission/devops/advisor` 双参、`dag(spec, reportMapping)` 一份映射发所有节点、`deliver(reportMapping)` → `{prefixUntil:0, thenMap:reportMapping}` | ✅ |
| 公开基类 | `Description.fs` + `ToolHost.fs`：新增 `baseClassResume`/`baseClassCommission`/`baseClassDevOps` 三槽位 | ✅ |
| 资源 | `base-class`/`base-class-fork`/`base-class-advisor`/`base-class-lifecycle`/`deliver`/`delegation-rules` 改写；新建 `base-class-resume`/`base-class-commission`/`base-class-devops`（均双语）；`js-plan` 改述 | ✅ |
| 能力 | `Js/Capability.fs` 的 `Resume`/`Commission`/`DevOps` 投影；`GeneratorSurface.fs` 标签；`RuntimeSurface.fs` 的 `capabilityOfLabel` | ✅ |

### 迁移过程中发现并修掉的**真实缺陷**（不是文字对齐）

1. **跨 realm 载荷被丢弃**（致命）：JS 程序在 `node:vm` 沙箱 realm 运行，其对象原型不是宿主 realm 的 `Object.prototype`，而 `ToolHostCodec` 的 plain-record 判定按 realm 比较 → 委派载荷字段全丢，所有 `this.fork(...)` 恒返回 `name-required`。修法：在委派接缝归一（`ToolRegistry.fs` 的 `plainRecordCopy` + `payloadWithRecord`），`fork`/`commission`/`resume`/`devops`/`fission` 全覆盖。修后实测：`this.fork({calling:'devops',name:'Op',charge:'x'},{1:null})` → `calling-conflict`；`name:'devops'` → `assessment-pending-for-devops`；`name:'Op'` → `ok=true, outcome="created"`。
2. **fork 结构化路径丢了 DevOps 评审门的拒绝特异性**：原先一律报 `placement-failed`。现与 resume 共用一套 `refusalCode` 映射（`assessment-pending-for-devops` / `dispatch-closed` / `name-required` / `calling-conflict` / `unknown-calling` …）。
3. **Manager 生成面丢了 ultraExample**：`ultraExample` 的 manager 能力子集缺 `Resume` → Manager 的示例消失（025 的 J01 红）。已补。
4. **失败调用抹掉先前合法交付**：`ToolWorkflow.fs` 的 `JsDeliveredValues.clear sessionId` 改为 `clearOwnDelivery`（只清围栏正好等于本次调用者）。
5. **失败程序留下可呈现的视图声明**：顶层参数改为**结算成功后**才提交（`ToolHost.fs`）。
6. **交付格收紧为两级**：带围栏 cell = 交付证据；无围栏 cell = 渲染缓存；`restoreMaterial`（任意成品字符串入口）已删除。
7. **见证读取因「同 (session, run) 多条」而丢证据**：`TaskDelivery.tryFind` 改为 `tryFindByFence` / `tryFindByFenceFields` 围栏感知读取；三处调用方（`DelegationJournalAdapter` / `Fission/Host` / `ChildRecoveryWorkflow`）已切。
8. **见证生产者为零**：`TaskDeliveryJournal.commit` 原本**全仓零调用方**。现由 `ToolRegistry.settlementOf` 装配归属、`OpenCode/Host/DeliveryWitnessWriter.fs`（新 shard `Wanxiangshu.Owner.host-boundary.opencode-host-deliverywitnesswriter`）唯一实现写入器、`ToolWorkflow.settleDeliveredView` 在提交成功后才写交付格。
9. **交付停止位置错**：原先在 deliver 工具内部 + event 钩子物理中断（user-facing 会话上必被 `MANAGED-SESSION-016` 拒绝）。现改为 **transform 钩子**的 `delivery-adjudication` stage：抑制下一次 provider step + 清空消息 + 中断本次 attempt；「已发出」与「已观察到」分别取证。
10. **SyncDelegate 把「完成但未交付」判成失败**：改为与 fork 侧一致的**未解除义务**（父方可重派）。

### 测试与文档

- 新增：`requirements/repository-programming/tests/063.test.mjs`（顶层扁平参数 + `view` 拒绝）、`064.test.mjs`（另一会话产物）、`requirements/context-compression/tests/032.test.mjs`（reportMaterial 纯投影）、`requirements/intra-participant-parallelism/tests/018.test.mjs`（fission 上下文继承）、`requirements/semantic-trace/tests/013.test.mjs`、`requirements/effect-accounting/tests/013.test.mjs`。
- 迁移：`repository-programming/056`（委派成员带 reportMapping + 两条拒绝用例）、`advisor/006`、`capability-enforcement/009/024/025/026`（从退役 host 工具迁到编程面成员）、`support/manager-programming.mjs`（J01 能力集含 Resume、api 绑定键含 resume、J13 拒绝用例）、`work/030`/`031`、`delegation/027`。
- 交付形态迁移：`repository-programming/030/054/059/060`、`e2e/scenarios/long-stroke.toml`、`context-compression/031`（改扁平参数 + 新增「失败不提交声明」用例）。
- WHAT：`delegation`、`advisor`、`context-compression`（+新条款 032）、`repository-programming`、`work`（[011][021][024][026][028][030][032]）、`intra-participant-parallelism`（新条款 018）、`managed-session-lifecycle`（[016]）、`semantic-trace`（新条款 013）、`effect-accounting`（新条款 013）、`GAP.md`、`INDEX.md`（条款计数同步）。

## 八·三 当前验证状态（**结论绑定于此刻的代码**）

- **全量 build 尚未重跑**。上一次全量 build 是红的，5 个文件破损（`ToolRuntimeScope.fs`、`ToolWorkflow.fs` 的 `.fsi`、`PluginTransformSurface.fs`、`OrdinaryTurnWorkflow.fs`、`Fork/Host/RunLifecycle.fsi`）—— 已由 5 个 repair agent 分别用 `node scripts/compile-owner.mjs <shard>.fsproj` 验证**各自 shard 编译通过**。**全量绿是下一步第一件事**。
- 委派迁移期实测：`node --test requirements/capability-enforcement/tests/{002,006,009,010,011,024,025,026}.test.mjs` → **110 tests / 100 pass / 0 fail / 10 skipped**（10 条是未开 integration tier 的）。
- **未跑**：`npm run check`（全门禁）、fantomas 格式化、目标套件全量、integration tier。
- **既有红（非本任务造成，基线已存在）**：
  - `requirements/delegation/tests/026.test.mjs` —— 在**迁移前**的提交 `842a144af` 上以同样方式挂死（`Promise resolution is still pending but the event loop has already resolved`）。已用独立 worktree 实测确认，**不是**本次迁移造成的。
  - `requirements/capability-enforcement/tests/006.test.mjs` 的两条 Pair Hint 断言 —— 自 `60f0802fd`（锚点头标记改变字节偏移）起恒红；本轮已改成语义断言（不钉死字节偏移），整文件 16/16 绿。

## 八·四 未提交的新增文件

```
src/Wanxiangshu/OpenCode/Host/DeliveryWitnessWriter.fs        （新）
src/Wanxiangshu/OpenCode/Host/DeliveryWitnessWriter.fsi       （新）
src/Wanxiangshu/Wanxiangshu.Owner.host-boundary.opencode-host-deliverywitnesswriter.fsproj （新）
requirements/repository-programming/tests/064.test.mjs        （另一会话产物，需其属主确认）
requirements/context-compression/tests/032.test.mjs           （本轮）
requirements/intra-participant-parallelism/tests/018.test.mjs （本轮）
requirements/semantic-trace/tests/013.test.mjs                （本轮）
requirements/effect-accounting/tests/013.test.mjs             （本轮）
```
另有 `src/Wanxiangshu/compile-order.txt` 登记了 `OpenCode/Host/DeliveryWitnessWriter.fsi`/`.fs`（新 shard 的全序位置）。

**不属于本任务、提交时不要带上**：`debug/`（另一会话的会话转储）、`task.md`（另一会话的任务书）。

## 八·五 剩余任务（按顺序做）

### R1 编译绿（第一件事）
1. 等飞行中的两个 agent 落地：`PlanDeliverReceiptShape`（`Mission/Planning/PlanTools.fs` 交付回执形状）、`FixPluginTransformSurface`（`OpenCode/Host/PluginTransformSurface.fs` 补 `AdjudicateDelivery` 能力桩）。
2. `node scripts/build.mjs` → 必须 `build ok`。若仍红：按错误所在 shard 用 `node scripts/compile-owner.mjs <shard>.fsproj` 定位（这是定向编译检查，不是项目门禁）。

### R2 格式化
`dotnet tool run fantomas src/Wanxiangshu` —— 本轮触碰的 .fs/.fsi 很多（49 个 src 文件），**在阶段末统一跑一次**，不要让 agent 各跑。

### R3 目标测试全绿
```
node --test requirements/capability-enforcement/tests/*.test.mjs
node --test requirements/repository-programming/tests/*.test.mjs
node --test requirements/advisor/tests/*.test.mjs
node --test requirements/delegation/tests/{019,021,024,027}.test.mjs   # 026 是既有红
node --test requirements/work/tests/*.test.mjs
node --test requirements/context-compression/tests/*.test.mjs
node --test requirements/semantic-trace/tests/013.test.mjs
node --test requirements/effect-accounting/tests/013.test.mjs
node --test requirements/intra-participant-parallelism/tests/018.test.mjs
WXS_TIER_INTEGRATION=1 node --test requirements/capability-enforcement/tests/025.test.mjs
```

### R4 全门禁
`npm run check`（含 architecture / subsystems / language-parity / ablation-manifest / tool-referential-integrity / js-surface-manifest / clean-break-symbols 等）。注意两个新风险点：
- **新跨子系统边** `host → application-composition`（`repository-programming-js-toolbindings` → `opencode-tools-toolruntimescope`，为 `DeliveryAttemptLedger`）—— 使最大 SCC 由 10 升到 16。`scripts/checks/subsystems.mjs` 只报不失败，但**层向需要复核**。
- `resources/ablation/tool-map.json` 与 `StaticTools.knownToolNames` 必须保持**双射**（`scripts/checks/ablation-manifest.mjs`：`tool-unmapped` / `tool-stale`）。

### R5 提交并推送
只提交**本任务与已合流**的文件；**排除** `debug/` 与 `task.md`。分两次提交更安全：先「委派接口迁移与交付语义改造」（src + resources + requirements），再「任务书与索引同步」。

### R6 已识别、尚未做完的遗留（**不要**当成已完成）

| # | 遗留 | 位置 | 影响 |
|---|---|---|---|
| a | **Plan 路径没有 settlement** | `ToolRegistry.fs` 的 `runPlanProgram` → `JsToolWorkflow.execute`（无围栏归属） | Plan 的 `this.deliver` **不产生** durable 见证；Plan 侧只有进程内交付格 |
| b | **post-S3 交付未登记「已成立」** | `ToolRegistry.fs` 的 `deliverPostS3`（S1/S2/S3 → `PlanTools.startPostS3Execution`）不调登记；只有 `deliverCursor` 调 | 该 post-S3 交付**不会**被 transform 停住（旧代码也没中断过它，非回归，但新契约下应登记） |
| c | **`office-capability/WHAT.md` [021] 措辞** | 仍写「Plan 的工作工具权限集为 todowrite-for-plan、devops、review、deliver」 | 与「devops 已从宿主注册工具退役」的口径需统一（权限集本身仍含 `DevOpsConsultation`，不矛盾，但措辞易误读） |
| d | **测试 README 缺条目** | `requirements/semantic-trace/tests/README.md`、`requirements/effect-accounting/tests/README.md` 缺 013 | 证据页未登记（README 约定是「说明当前证据，不增加合同」） |
| e | **durable 读只判「本 session 有任一见证」** | `ToolRuntimeScope.fs` 的 `hasDurableEstablished` | 粗判；精确围栏判定在 `tryFindDurableEstablished` |
| f | **交付回执形状刚落地未验证** | `Mission/Planning/PlanTools.fs` 的 Relayed/Delivered 说明 + `NextIncumbencyId` 真实填充 | 条款 5 的「原因/尚欠/下一步」是否真的到模型眼前，**未跑过测试** |
| g | **新 shard 的层向** | `opencode-host-deliverywitnesswriter` | 见 R4 |

## 八·六 续接命令

```bash
node scripts/build.mjs                                   # 全量 Fable 编译
node scripts/compile-owner.mjs <shard>.fsproj              # 定向编译某个 shard
node --test requirements/<包>/tests/<NNN>.test.mjs          # 单条用例
WXS_TIER_INTEGRATION=1 node --test requirements/host-boundary/tests/032.test.mjs   # integration canary
npm run check                                            # 全门禁
dotnet tool run fantomas src/Wanxiangshu                 # 格式化（阶段末一次）
```

## 八·七 纪律提醒（承接上文六节）

- 同一工作树有**第二个会话**在施工：每次写前必须重新读文件；`git status` 里的改动**未必**都是本任务的。
- 验证结论绑定代码状态：并行改动落地后，先前绿轮不作数，**最终 build + 测试必须重跑**。
- 提交前确认没有残留临时文件、调试打印、未跟踪生成物（`debug/` 是另一会话的，别带）。
- park 现场必须当场 unpark/abandon 闭环。
