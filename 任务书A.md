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

---

# 九、第二个会话（deliver 语义会话）停电前盘点 · 2026-10-20 21:2x

本节由**第二个会话**（读 `debug/AGENTS.md` 的 DevOps 转储、按 `task.md` 六条交付合同排查的一会话）追加。上文八节是委派迁移会话的盘点；本节记本会话的结论、已落盘改动、**未做完的剩余任务**与续接命令。两会话共用同一工作树，改动互相交叠。

## 九·一 本会话的起因与结论

- 起因：`debug/AGENTS.md`（DevOps 会话转储）暴露两类万象术缺陷：① user-facing 的 devops 调 `deliver` 后**没有 interrupt**，控制权没回到用户；② 一个 park 现场（park-1）**反复 abandon 也清不掉**，因而交付被泄漏兜底门禁一直拒绝。
- 根因（读码确认，非转述）：
  - ① `ToolHost.fs` 的 `inputOf` 与 `ToolWorkflow.fs` 的 `parseWorkflowInvocation` **按优先级静默丢弃**三态入参（program > unpark > abandon），且 Host schema 把 `program` 标成必填 → 模型**结构上无法**单独发 `abandon`；park 现场因此永远不被消费，交付门禁常闭。
  - ② 交付见证（`TaskDeliveryWitness`）**全仓零生产者**（`TaskDeliveryJournal.commit` 无调用方）；`OrdinaryTurnWorkflow.longWorkGate` 又把「存在交付」当作「已终态」而 `Wait` → **任何交付过的会话都发不出终态**；编程面 `this.deliver` 的中断原本根本不在宿主侧发生。
- 契约依据：`task.md` 六条（deliver 只接受 view／先散文后 deliver／宿主决定是否继续／无需继续时在 transform 钩子 interrupt／仍需继续时在 deliver 返回值说明／提交·呈现·中断分开取证）。**注意**：`task.md` 写的是 `deliver(view)`，而仓库现行规范（`requirements/repository-programming/WHAT.md` [053]/[054]/[059]）已改为 `deliver(reportMapping)` + 顶层扁平 `prefixUntil`/`thenMap`；**以仓库现行规范为准**，`task.md` 的「只接受 view」语义被 [054] 的「reportMapping 降级」实现所承接。

## 九·二 本会话已落盘的改动（工作树，未提交）

### 1. 编程面三态互斥（park 现场可闭环）

- `Repository/Programming/Js/OpenCode/ToolHost.fs`：`inputOf` 改为**忠实投影**（program/unpark/abandon 各出现即拷入，不再有优先级丢弃）；`Execute` 在**捕获视图声明与执行程序之前**先分类，冲突输入 → `INVALID_ARGUMENT`（reason 点名冲突字段，零副作用）；schema 全部改为可选（`optionalStringSchemaDescribed` / 新增 `optionalObjectSchema` / `optionalRecordSchemaOf`）。
- `Repository/Programming/Js/OpenCode/ToolWorkflow.fs`（+`.fsi`）：`parseWorkflowInvocation` 改为**计数式**（恰好一个态；0 个 → 既有 missing-program 文案；2~3 个 → `Invalid reason`）；新增公开 `conflictingInputReason : obj -> string option` 供宿主层复用同一句 reason；删除无调用方的 `resumeParked`/`abandonParked`。
- `OpenCode/Codec/ToolHostCodec.fs`（+`.fsi`）：新增 `optionalObjectSchema` / `optionalRecordSchemaOf`。
- 资源：`resources/provider/tool/js-program/park/*` 之外未新增；WHAT 新增 `requirements/repository-programming/WHAT.md` [064]「调用面三态互斥」+ README 段落。
- 测试：`requirements/repository-programming/tests/049.test.mjs` 重写（参数名单 `['program','unpark','abandon','prefixUntil','thenMap']`、三者皆可选、假 schema 支持 `optional()`）、新增 `064.test.mjs`（冲突调用可见失败、探针文件未落盘、handle 未被消费、三态各自单独可用）、`005/016/063` 的假 schema 补 `optional()`。

### 2. 交付语义与交付回执（`deliver` 合同）

- `Repository/Programming/Js/Description.fs`：`deliver(reportMapping)` 在**成员边界**拒绝多参（`arguments.length > 1`）与缺参，返回 `{ok:false, code:"VIEW_DECLARATION_INVALID", reason}` 数据信封，程序不失败也不交付。
- `Mission/Work/OpenCode/DeliverTool.fs`（+`.fsi`）：
  - **确认围栏重放**：`enterDeliver` 增加「已落盘 ConfirmationCommitted 围栏」分支，排在 active tenure 之前 → 任期接力或交付之后重放同一 fence，返回**已记录的确认**而不是 `assessment_required` / `BindingUnavailable`；
  - **关闭失败不再吞掉中断**：`appendAuthorityLogicalRunClosed` 的异常被捕获，仍先 `RegisterEstablishedDelivery`（arm）再抛出 → 已提交退休不被抹掉，中断不被跳过；
  - 删除死代码：`Path.NoRetirementProjection` 字面量、`requireSome`、硬编码空 `pending`（`deferred` 永不可达）、`confirmationResult(prepared)` / `refreshAfterFreeze(context)` 参数；资源 `resources/provider/tool/deliver/finish-not-recorded/{en,zh-CN}.md` 删除；`confirmation/{en,zh-CN}.md` 改写为只承诺「第一次记确认、第二次才结束」；
  - 退休结果带 **outcome**（`Relayed` / `Delivered`），不再两态同一句话。
- `Repository/Programming/Js/DeliveredValues.fs`：`recordView`（无围栏渲染缓存）**不再覆盖**带围栏的交付格——降级会抹掉拥有该交付的那次调用。
- 测试与资源迁移：`requirements/repository-programming/tests/054|059`、`requirements/concern-routing/tests/003`、`resources/provider/role/{devops,engineer}`、`resources/provider/work/{devops-continue,engineer-continue}`：全部改为 `deliver(reportMapping)` 口径；`deliver(undefined)` 期望码改为 `VIEW_DECLARATION_INVALID`。

### 3. 交付见证（有生产者）与停止位置（transform 钩子）

本会话补齐的部分（另一会话同期落地了 `OpenCode/Host/DeliveryWitnessWriter.fs` 新 shard，`ToolRegistry.fs:873` 已是唯一写入器，二者不重复）：

- `OpenCode/Tools/ToolRegistry.fs`：`settlementOf ctx` 装配交付归属（active profile 的 LogicalRunId/AuthorityRoot、快照里该 provider run 的 assistant 消息 `ParentId` 作物理围栏、`Text` 段作调用前散文、handle/invocation 作任务引用）+ 见证写入器；生成面 Execute 成功后按**精确围栏** `runtime.RegisterEstablishedDelivery`。
- `Repository/Programming/Js/OpenCode/ToolHostSurface.fs`：非宿主测试面传 `settlementOf = None`。
- `Composition/Turn/OrdinaryTurnWorkflow.fs`：`hasDeliveryEvidence` 只认真实见证（有 journal 时只认 durable witness；无 journal 时只认带围栏的 cell），不再用「存在交付」把会话永久静音。
- `OpenCode/Tools/ToolRuntimeScope.fs` + `OpenCode/Host/HostTurnObserver.fs`：`DeliveryAttemptLedger` 进程内台账（已成立／已呈现／中断已发出／中断已观察到 分别取证），turn 观察者按精确 run 取自有中断任务。
- `OpenCode/Plugin/PluginTransforms.fs`：新增 **delivery-adjudication** stage（条款 4）：本次 attempt 存在已成立交付且物理输入未变时，抑制下一次 provider step → 发出本次 attempt 的中断 → 清空本次请求消息，LLM 因此看不到 deliver 回复；`RelayNarrativeTransform.clearRequestMessages` 抽出共用动作；删除重复的 `handleInterruptionError` 死定义；stage-4 注释改为与代码一致（不重写消息集）。

### 4. 交付消费方（fork / fission / sync delegate）

- `Execution/Delegation/Fork/Host/RunLifecycle.fs` + `Runtime.fs`：视图读取改为**非破坏性 peek**（围栏必须命中本 run），**所有权守卫先于取用**（外来/重放终态不花掉活运行��材料），写入 `HandleCompleted` 前**要求围栏匹配的 durable 见证**（无 journal 的 harness 路径保持可用），视图读取故障在 `Runtime.Complete` 侧独立隔离（不再把已证明的终态变成 Failed）。
- `Execution/Fission/OpenCode/Host.fs`：**先发布 owner 终态、再写 `FissionConverged`**；发布失败不写 Converged，失败路径得以 abort lane、通知 owner、释放 owner 预留与运行态。
- `Execution/Delegation/SyncDelegate/Runtime.fs`（+`Surface.fs`）：委派子会话被交付中断后终态可能是 `TurnAborted` → 新增该分支，**有已提交交付即按成功结算**；消费改为**先读 durable 见证**、进程内 cell 兜底；中断**先于**物理租约释放；harness 记录 `(sessionId, providerRun, kind, refused, reason)` 并可注入拒绝，新增 `awaitDeliveryInterrupt` 等观察缝。

## 九·三 本会话的验证状态（绑定当前代码）

- **全量 build 结果（停电前跑完，日志 `/tmp/build-handoff.log`）**：F# 侧**编译通过**（`[owner-compile] OK: Wanxiangshu.Impact.fsproj`，1310 项），`js-surface-manifest: OK`；**卡在 JS 链接门禁**：`js-module-linkage: FAILED — 15 violation(s)`，15 条全是同一条根因——
  `OpenCode/Host/HostTurnObserver.js: fable_modules/fable-library-js.5.13.0/Task.js is missing named export 'whenAny'`
  （其余 14 条是 `HostTurnObserver` / `PluginHooks` / `PluginTransforms` / `PluginHostWiring` / `SyncDelegate.Surface` / `SpikePlugin` / `Sphinx.V2.Hosts.OpenCode.Surface` 等**传递依赖**它的模块加载失败）。
  根因：新写的 `Task.WhenAny([| guard; delivery |])`（`HostTurnObserver.fs` 等待「守卫中断」与「交付中断」任一结算）在 Fable 5.13 的 `fable-library-js` 里**没有 `whenAny` 导出**。仓库既有先例：`ToolWorkflow.fs` 的注释写明「Fable has no Task.WhenAny; Promise.race is the established pattern (Await.fs / FinalityController)」。
  **修法**：改成 `emitJsExpr (a, b) "Promise.race([$0,$1])"` 形态（与 `SyncDelegate/Surface.fs` 的 `NodeTiming.timerTask` + `Promise.race` 同款），不要引入 `Task.WhenAny`。
- 停电前**全量 unit 基线**（`node requirements/verification-system/tests/run.mjs`，57 条失败，退出码 1）集中在三类：
  1. **迁移遗留的红**：测试仍调用已退役的宿主工具（`hooks.tool.devops` / `fork` / `resume`）或旧的 `deliver(view)` 形状 → `TypeError: Cannot read properties of undefined (reading 'execute')`、断言 `deliver(value)`；涉及 `capability-enforcement-009/026`、`delegation-023/025`、`knowledge-reuse-010`、`managed-session-lifecycle-004/014/022`、`intra-participant-parallelism-003/010`。
  2. **交付语义相关**：`context-compression-031`（`view` 参数退役后的 schema 断言）、`concern-routing-003`（deliver 资源措辞，本会话已改）、`repository-programming-054/059`（本会话已改）。
  3. **门禁/词法类**：`delegation-029`（`ChildRecoveryWorkflow.fs` 不得直接引用 `AgentJournal` 的词法审计）、`js-semantic-surface-002`（边界门禁）。
- 本会话**未跑**任何测试套件（子代理一律不跑门禁）；本会话改动的定向用例也**未执行**。

## 九·四 剩余任务（续接后按此顺序做；每条都给出位置与验收）

### A. 编译与门禁（阻塞其余一切）

- **A1** `node scripts/build.mjs` 必须 `build ok`。若红：按错误所在 shard 用 `node scripts/compile-owner.mjs <shard>.fsproj` 定向定位（不是项目门禁）。**注意**：`Description.fs` 的生成面改动由一个被中途叫停的代理落地，编译后要复核 `deliver`/`resume`/`commission`/`dag` 四段是否自洽。
- **A2** `dotnet tool run fantomas src/Wanxiangshu`（阶段末一次，不要让代理各跑）。
- **A3** `npm run check`（architecture / subsystems / language-parity / ablation-manifest / tool-referential-integrity / js-surface-manifest / clean-break-symbols）。两个已知风险点见八·五 R4（新 shard 层向、`tool-map.json` 与 `StaticTools.knownToolNames` 双射）。

### B. 交付面剩余缺陷（本会话审计已定位、**未修**）

- **B1** ~~`resume`/`commission` 不派生命名~~ —— **已闭合（2026-10-21 核实，且非本批引入）**：`fork` 与 `commission` 共享 `deriveNameFromCharge`（`Description.fs:429`，`:565-571`、`:575-580`）；`resume` **有意不派生**（`Description.fs:581-585`，无 `name_derivation` 槽），因为现行 WHAT（`repository-programming` [056]）规定 `resume(payload, reportMapping)` 的 `payload` **必带 `name`**，拒绝码含 `name-required`。**旧条目「resume 应照抄 fork 派生」是过时义务，不得恢复。**
- **B2** 同文件：`dag` 门控仍是 **Fork 即可**，但方法体调用 `this.join()`（Join 才有）→ 生成面可能引用不存在的成员。修法：dag 需要 Fork ∧ Join。
- **B3** `Repository/Programming/Js/Capability.fs`：成员注册表**缺 deliver 分片** → `deliver` 不在 `surface.members` / `memberBinding`，四层目录描述不到它。
- **B4** ~~Manager 运行时基类读 `api.reviewOnly` 导致 TypeError~~ —— **已闭合（2026-10-21 核实，且非本批引入）**：`ReviewOnly` 子对象的成员读 `this._api`（构造器收到的就是 `api.reviewOnly` 命名空间本身），不再误访问 `this._api.reviewOnly`（`Description.fs:771-778`）；缺失成员由 `missingMemberGuard`（`Description.fs:440-448`）以 `{ ok: false, code: "REVIEW_ONLY", reason: ... }` 拒绝；Manager 面在 `Description.fs:402` 挂 `reviewOnly = new ReviewOnly()`。
- **B5** `Repository/Programming/Js/DeliveredValues.fs`：`tryTakeRenderedFor` 只比 `ProviderRun`，**不比 `ToolCallId`** → 同一 provider run 内的另一笔调用可取走本调用的交付物。修法：加按完整围栏取用的入口，fork/sync 侧改用它。
- **B6** `Host/Facts.fs`：`tryFindByFenceFields` **不比较 `LogicalRunId` 与 `Task`** → 别的逻辑运行/别的任务的见证可以满足门禁。修法：围栏子集读取补这两个过滤位。
- **B7** `Mission/Work/OpenCode/DeliverTool.fs`：`receipt.Status = "Observed"` 被当作 durable outcome 接纳（`durableOutcome`）→ 「无新事实的观察」可能走成 Delivered 并触发中断。修法：只认 `Committed`/`Replayed`。
- **B8** `Mission/Work/OpenCode/DeliverTool.fs`：S4 终态中断**未按当前物理输入设围栏**（`ask` 路径有先后两次核对，S4 没有）→ 交付与新用户输入之间存在误停窗口。
- **B9** `OpenCode/Plugin/PluginTransforms.fs` + `Mission/Work/OpenCode/NarrativeTransform.fs`（叙述审计遗留）：
  - 退休 attempt 的「中断」回调现在接的是 `ManagerWorkflow.continueAfterRetiredAttempt`——它**顺带派发后继提示**，与「停止」不是一件事；应拆开（停止与接力分别取证）。
  - `physicalUserMessageId` 取不到时直接 `invalidOp`，把「停止」变成「钩子失败」；应跳过步抑制但仍尝试中断。
  - `staleRetirement` 只认 `Outcome = "Relayed"`；**终态 `Delivered`（无继任者）**的残留请求不算 stale → 会在无权威的任期上继续跑。
  - `relayedCut` 取**第一条** Relayed，而 `afterCut`/`cutPresent` 用**最后一条** cut → 多轮接力时围栏错位。
  - `acceptedSuccessorRequestFor` 只判「存在 ChatExecution」（含终态、任意逻辑运行）→ 继任判据是竞态而非事实；应要求当前逻辑运行下的**活跃接纳**。
- **B10** `requirements/work/tests/011.test.mjs:229` 的 `test.todo`（在途中断未完成前不得派发后继）**仍是 todo**；`requirements/work/tests/049.test.mjs` 的中断断言是**空断言**（钩子键写的是 `message.part.updated`，插件注册的是 `event`）→ 两条都要转成真断言。

### C. 交付面剩余缺陷（另一会话已识别、仍未闭合）

- **C1** Plan 路径**没有 settlement**（`ToolRegistry.runPlanProgram` → `JsToolWorkflow.execute` 无围栏归属）→ Plan 的 `this.deliver` 不产生 durable 见证，只有进程内交付格。
- **C2** post-S3 交付**未登记「已成立」**（只有 S4 Delivered 登记）→ 新契约下该交付不会被 transform 停住。
- **C3** `Execution/Fission/OpenCode/Host.fs:574-760` 的宿主侧 lane 交付是**死代码**（`deliverOneLane` 无调用方）；lane→owner 的**跨会话搬运**搬的是**声明**而非材料（锚点空间不跨会话），搬运还会留下同一交付两份可见。
- **C4** `requirements/repository-programming/tests/README.md` 等证据页的条目与新参数名不同步（049 参数名单）。

### D. 测试面剩余（迁移遗留）

- **D1** 仍调用退役宿主工具的用例迁移到编程面成员：`capability-enforcement-009/026`、`delegation-023/025`、`knowledge-reuse-010`、`managed-session-lifecycle-004/014/022`、`intra-participant-parallelism-003/010`。
- **D2** `delegation-029` 的词法审计（`ChildRecoveryWorkflow.fs` 不得直接引用 `AgentJournal`）与 `js-semantic-surface-002` 边界门禁需按新依赖边复核。
- **D3** **补本会话缺陷的回归用例**（目前一条未写）：
  - 三态冲突可见失败 + handle 未被消费（已写：`repository-programming/064`）；
  - 交付 → 见证落盘 → transform 停住该 attempt（**未写**，这是条款 3/4 的核心闭环）；
  - 提交/呈现/中断三件事分别取证（**未写**）；
  - fork 侧重复读取同一终态不掏空交付格（**未写**）；
  - sync delegate 被中断后仍以交付视图结算调用（**未写**）。

## 九·五 续接命令

```bash
# 1) 编译（先看上一次日志尾部：/tmp/build-handoff.log）
node scripts/build.mjs
node scripts/compile-owner.mjs <shard>.fsproj      # 定向编译某 shard

# 2) 目标套件
node --test requirements/repository-programming/tests/064.test.mjs
node --test requirements/repository-programming/tests/{054,059}.test.mjs
node --test requirements/work/tests/{011,026,027,032,033,049}.test.mjs
node --test requirements/delegation/tests/{023,025}.test.mjs
node --test requirements/capability-enforcement/tests/{009,026}.test.mjs

# 3) 全量
node requirements/verification-system/tests/run.mjs   # unit（停电前基线：57 红）
npm run check
dotnet tool run fantomas src/Wanxiangshu
```

## 九·六 本会话的教训（写给下一个会话）

1. **模型看到的工具 schema 会决定它能表达什么**。`program` 被标成必填，模型就永远无法单独发 `abandon`——「模型没按对方式调用」常常是宿主契约逼出来的，不是模型错。
2. **静默丢弃比报错更贵**：三态入参的「优先级」写法让每一次 abandon 都变成一次普通 program 运行，现场永远不清，交付门禁常闭，症状看起来完全不像参数解析问题。
3. **交付格/见证/呈现是三件事**，用同一个布尔量（`hasDelivered`）判断必然互相污染：见证是完成证据，格子是材料缓存，呈现是给调用方的回执。
4. **「发出中断」≠「中断已生效」**：user-facing 会话上的 `InterruptAttempt` 会被 `MANAGED-SESSION-016` 拒绝；把拒绝吞掉再宣布「已停止」，等于什么都没停。本会话的落法是：`已发出` 与 `已观察到` 分别登记，停止动作放在 transform 钩子（清空消息 + 抑制下一次 step + 中断本次 attempt），让 LLM 看不到 deliver 回复。
5. **交付发生在宿主回合之下**：调用前散文、逻辑运行、物理用户消息、任务归属在 settle 点都看不见，必须由宿主回合层注入；编不出来就别硬编（`DeliveryAttribution` 缺字段即视为「未成立」）。
6. 验证结论必须绑定代码状态：并行会话落地后，先前绿轮不作数；**最终 build + 测试必须重跑**。
7. 派子代理时明确「只改指定文件、不跑 build/测试/格式化」，并要求回报 `file:line` 级证据；否则代理会在同一工作树里互相覆盖（本会话两次遇到 `Description.fs` / `DeliverTool.fs` 被并行会话改写）。

## 十 验收状态与剩余（2026-10-21 收口会话实测）

本节由收口会话实测写入，记录**当前代码状态下的真实门禁与套件结果**，以及仍未闭合的剩余项。上文一到九节是历史施工书；本节只陈述事实，不宣称整体任务完结。

### 十·一 已交付并推送（commit 链，旧→新）

| commit | 内容 |
|---|---|
| `0b197230a` | `docs(js-program): document the devops return shape as { ok, kind, status, view }` |
| `e8c44bd8b` | `feat(js-program)!: derive the permission table from one label function and add the deliver member` |
| `35f9a6f4f` | `feat(context-compression)!: read the checkpoint declaration as flat prefixUntil and thenMap` |
| `0a742980c` | `feat(delivery): add the logical run and the delivery task to the establishment fence` |
| `4d80422e8` | `docs(requirements): restate the GAP rows under the current declaration and delivery semantics` |
| `5232b0eba` | `refactor(delivery): drop the unused full-fence take and state the fallback reader's limits` |
| `031ffb11d` | `style: format the files this change touched` |

已闭合对应上文条目：**B2**（dag 需 Fork ∧ Join，上游 `3ee7dcdaa`）、**B3**（成员注册表补 deliver 分片）、**B6**（围栏补 `LogicalRunId` 与 `Task` 两位过滤位）、**B7**（`Observed` 不再当 durable outcome，`DeliverTool.fs:396` 已按 work-015 注释处理）。

### 十·二 门禁实测

- `node scripts/build.mjs` → `build ok`；`js-surface-manifest: OK — 151 registered surfaces`；`js-module-linkage: OK — 725 emitted modules linked and loaded`。
- `dotnet tool run fantomas src/Wanxiangshu` → 全仓 55 个文件需要格式化。本任务只格式化**本任务触碰的 16 个文件**并单独提交（`031ffb11d`），其余 39 个是上游既有欠账，未动。格式化前 15/16 个文件在上游基线已是未格式化状态，**唯一由本任务引入未格式化的是 `OfficeCapabilitySurface.fs`**。
- `npm run check` → **红**。`check.mjs: FAILED with 167 issue(s) across 2 gate(s)`：
  - `scripts/checks/fsharp-control-pyramid.mjs`：**100** 处 violation；
  - `scripts/checks/js-boundary-gate.mjs`：**67** 处 violation。
  对照本批上游基线 `435eac2d9`（同两门禁 **96 + 67 = 163**），本批**净增 4 处**，全部在 `src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs`（`branch-pyramid` 0→3、`match-pyramid` 3→5），源于本批新增的扁平声明判定（`PluginHooks.fs:425`、`:442`、`:444`、`:451`、`:454`）。其余 163 处为上游既有欠账。
- `node scripts/checks/language-parity-gate.mjs` → `OK — 266 semantic resource(s)`。

#### 十·二·一 控制金字塔净增归零（`PluginHooks.fs`，唯一编辑文件）

`src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs`：**+43 / −47**，唯一改动文件。扁平声明判定从 `viewTerminalObservation` 的内联判定提为两个模块级纯 helper：

| helper | 位置 | 职责 |
|---|---|---|
| `declarationOf` | `PluginHooks.fs:89-102` | 从扁平参数 `prefixUntil` / `thenMap` 组装声明对象；两个键皆缺则无声明 |
| `declaredViewOf` | `PluginHooks.fs:112-120` | 声明有效才计数：冲突输入 → false，无声明 → false，声明非法 → false，否则 true |

两处复用既有判定，不复制三态计数：`conflictingInputReason`（`Repository/Programming/Js/OpenCode/ToolWorkflow.fs:1201-1207`，问调用面自己的法）、`AnchorSurface.parse`（`Repository/Programming/Js/Anchor/AnchorSurface.fs:70-71`）。原内联的 `isPresent (input?program / ?unpark / ?abandon)` 三态重数随之删除，其 `isPresent` 私有 helper 已无调用方，一并移除。未用注释白名单压警告，未改任何语义：冲突 / 无声明 / 非法声明三条路径的判定结果与改前逐条相同。

- `node scripts/checks/fsharp-control-pyramid.mjs` → `PluginHooks.fs` 由 **8** 处降为 **3** 处（`:514`、`:556`、`:706`，均为上游既有欠账）；全局 **100 → 95**。本批净增 **4 → 0**。
- `node scripts/build.mjs` → `[build] build ok (generation 79)`；`js-surface-manifest: OK — 151 registered surfaces`；`js-module-linkage: OK — 725 emitted modules linked and loaded`。
- `WXS_TIER_INTEGRATION=1 node --test requirements/obligation-ledger/tests/002.test.mjs` → `tests 2 / pass 2 / fail 0`（含真实 OpenCode Host canary「installed OpenCode Host runs todowrite-for-engineer with view and commits ViewCheckpointCommitted per session」）。
- `node --test requirements/work/tests/020.test.mjs` → `tests 8 / pass 8 / fail 0`（含「a flat prefixUntil/thenMap declaration commits the durable ViewCheckpointCommitted, and a call without or with an invalid declaration commits none」）。

#### 十·二·二 C2 闭合：post-S3 交付登记「已成立」（绑定物理输入）

**缺陷**：`ToolRegistry.fs` 的 post-S3 交付路径没有登记「交付已成立」（提交面事实），因此 transform 钩子的交付裁决（`PluginTransforms.fs:678`，`tryFindEstablished sessionId physical`）永远不命中，该交付不会被 transform 停住。上一版登记（`registerPostS3IfEntered` → `RegisterPostS3Interrupt`）还有第二个缺陷：它**不带物理输入**，`RegisterEstablishedDelivery` 于是回落到 `currentPhysicalUserMessage`（**登记时刻重读**会话当前物理输入）——那正是把本次交付的停止瞄准后来新输入的窗口。

**改法**（唯一编辑文件：`src/Wanxiangshu/OpenCode/Tools/ToolRegistry.fs`；另删同文件族的死别名 `ToolRuntimeScope.fs` / `.fsi`）：

| 位置 | 改动 |
|---|---|
| `ToolRegistry.fs:1267-1296` | 新增 `boundPhysicalFor`：在**围栏内的自有 exchange 还在手上时**读出本次交付应答的物理输入（自有 assistant 消息的父消息是 user 消息时取父消息，否则取会话当前物理输入） |
| `ToolRegistry.fs:1298-1324` | `resolveDeliverRetirementCursor` 改为同时返回 `(retirementCursor, boundPhysical)`；围栏读取与 XTrace 捕获仍用同一份 fenced exchange |
| `ToolRegistry.fs:1334-1358` | `registerPostS3IfEntered` → `registerEstablishedDelivery`：登记**交付当时绑定**的物理输入（`physicalUserMessageId = physical`），登记时刻**不再重读** `CurrentPhysicalUserMessage`；绑定不出来的交付**不登记**（无法证明成立就不是已成立） |
| `ToolRegistry.fs:1391`、`:1430` | `deliverCursor` 与 `deliverPostS3` **两处**都调用它（此前 `deliverPostS3` 完全没有登记） |
| `ToolRuntimeScope.fs` / `.fsi` | 删除 `RegisterPostS3Interrupt` 别名（唯一调用方已改；该别名正是那个「回落到登记时刻重读物理输入」的入口） |

**实测**（临时探针只放 `/tmp`，已删除；`requirements/` 树下无残留）：

- post-S3 交付后 `DeliveryAttemptLedger.hasEstablished(session)` → **true**；`tryFindEstablished(session, phys('user-plan-root'))` → **HIT**（改前为 `null`、且记录的 `Physical` 为 `undefined`）；`tryFindEstablished(session, undefined)` → **null**；`tryFindEstablished(session, phys('user-new-input'))` → **null** ⇒ 物理输入已变则不再命中，**新输入不会被旧交付误停**。
- `node scripts/build.mjs` → `[build] build ok (generation 89)`；`js-surface-manifest: OK — 151 registered surfaces`；`js-module-linkage: OK — 725 emitted modules linked and loaded`。
- `node scripts/checks/fsharp-control-pyramid.mjs` → 与 HEAD 基线逐条 diff：**净增 0**（首版曾净增 1 处 `match → match`，已把登记判定摊平为单层 `match` 后归零）。
- `WXS_TIER_INTEGRATION=1 node --test requirements/work/tests/026.test.mjs` → `tests 2 / pass 1 / fail 1`。**仍红**，但红的**性质变了**：红点仍在 `026.test.mjs:124`（`assert.deepEqual(runtime.abortedIds, [sessionID], 'post-S3 deliver must interrupt the conversation')`）——该断言挂在**工具事件边界**（`hooks.event` 的 `message.part.updated`）上，而现行契约（`requirements/work/WHAT.md` [026] 与 [003]）规定**中断发生在 transform 钩子的交付裁决**，工具事件钩子只登记「呈现」面事实（`PluginHooks.fs:520-545` `settlePostS3Deliver` 只调 `RecordDeliveryPresented`）。把 `026` 的断言迁到 transform 裁决边界是**下一轮**的工作（B10/work-049 一并处理）。本轮只闭合 C2（登记「已成立」+ 绑定物理输入），未动断言。
- 回归：`WXS_TIER_INTEGRATION=1 node --test requirements/work/tests/020/022/023/024/025.test.mjs` → `tests 25 / pass 25 / fail 0`；`WXS_TIER_INTEGRATION=1 node --test requirements/obligation-ledger/tests/002.test.mjs` → `tests 2 / pass 2 / fail 0`（含真实 OpenCode Host canary）。

### 十·三 R3 套件实测

| 套件 | pass | fail | 判定 |
|---|---|---|---|
| `requirements/advisor/tests/*` | 14 | 0 | 绿 |
| `requirements/capability-enforcement/tests/*` | 133 | 0 | 绿 |
| `requirements/capability-enforcement/tests/025` @`WXS_TIER_INTEGRATION=1` | 33 | 0 | 绿 |
| `requirements/context-compression/tests/*` | 50 | 0 | 绿 |
| `requirements/semantic-trace/tests/013` | 6 | 0 | 绿 |
| `requirements/effect-accounting/tests/013` | 5 | 0 | 绿 |
| `requirements/host-boundary/tests/032` | 17 | 0 | 绿 |
| `requirements/host-boundary/tests/032` @`WXS_TIER_INTEGRATION=1` | 18 | 0 | 绿 |
| `requirements/work/tests/*` | 178 | **8** | 红，全部为上游既有红 |
| `requirements/intra-participant-parallelism/tests/018` | 1 | **1** | 红，上游既有红 |

**9 条红的 exact evidence（均在 pristine `435eac2d9` worktree 上复现出完全相同的失败，非本批引入）：**

| test | file:line | error |
|---|---|---|
| `work-009` the second deliver reports the live blocker instead of reporting an end | `requirements/work/tests/009.test.mjs:27` | `TypeError: Cannot read properties of undefined (reading 'execute')` |
| `work-011` physical prompt after Accepted deliver invalidates certificate… | `requirements/work/tests/011.test.mjs:225` | `TypeError: Cannot read properties of undefined (reading 'execute')` |
| `work-019` 没有任期时 read、review、fork、resume 在工具执行入口被拒绝 | `requirements/work/tests/019.test.mjs:42` | `TypeError: Cannot read properties of undefined (reading 'execute')` |
| `work-019` 有任期且评审尚未接纳时…派工和 deliver 在执行入口被拒绝 | `requirements/work/tests/019.test.mjs:70` | `TypeError: Cannot read properties of undefined (reading 'execute')` |
| `work-019` 评审已接纳且尚未确认时… | `requirements/work/tests/019.test.mjs:31` | `AssertionError: Missing expected rejection.` |
| `work-021` Plan separates human ask from devops investigation… | `requirements/work/tests/021.test.mjs:59` | `AssertionError: Tool catalog must contain registered devops tool` |
| `work-024` Plan admits its role tools on every stage… | `requirements/work/tests/024.test.mjs:33` | `AssertionError: plan must admit devops` |
| `work-026` S3 authorized deliver enters post-S3 state… | `requirements/work/tests/026.test.mjs:124` | `AssertionError: post-S3 deliver must interrupt the conversation` |
| `intra-participant-parallelism-018` a fission lane inherits the current session context… | `requirements/intra-participant-parallelism/tests/018.test.mjs:139` | `TypeError: Cannot read properties of undefined (reading 'execute')` |

`work-021` / `work-024` 的根因是上游把 `devops` **宿主工具**整体退役（`StaticTools.fs:52-54`：devops 离开目录与 ToolSpec，只以编程面成员 `this.devops` 存活），这些用例仍在断言已退役的宿主工具。其余 7 条同属「用例仍在调用已退役宿主工具/旧契约」。

**`work-026` 单列（可能不是「旧契约」，而是真生产缺陷 C2）**：`requirements/work/tests/026.test.mjs:124` 的 `AssertionError: post-S3 deliver must interrupt the conversation`。该红在 pristine `435eac2d9` 上同样存在，所以**不是本批引入**；但它与「用例仍调用已退役宿主工具」的其它 8 条不同类：**post-S3 交付是否登记「已成立」正是 C2 的内容**（`OpenCode/Tools/ToolRuntimeScope.fs:1333` 有 `RegisterPostS3Interrupt`，`PluginHooks.fs:533-544` 的 `settlePostS3Deliver` 只做 `RecordDeliveryPresented`）。build-doctor 正在审计 C2；**在 C2 审计结论出来前，不得把 `work-026` 归为「旧契约」豁免**。

### 十·三·五 `npm run check` 的 167 项去重分类

完整清单见 `/tmp/check-issues.md`（只读分类，未改任何代码）。摘要：

| 门禁 | 规则 | 数量 | A 生产违反 | B 测试旧表示 | C 词法误报 |
|---|---|---|---|---|---|
| `fsharp-control-pyramid.mjs` | `match-pyramid` / `branch-pyramid` | 100 | 100 | 0 | 0 |
| `js-boundary-gate.mjs` | `deep-dist-import` | 37 | 0 | 37 | 0 |
| `js-boundary-gate.mjs` | `du-shape` | 30 | 0 | 30 | 0 |
| **合计** | | **167** | **100** | **67** | **0** |

- **A 类 100 项**全部在 `src/`，去重后 24 个文件；其中 **本批净增 4 处**，全在 `PluginHooks.fs`（`:425`、`:442`、`:444`、`:451`、`:454` 的扁平声明判定链），其余 76 处为上游既有欠账。
- **B 类 67 项**全部在 `requirements/` 的 7 个测试文件里。**37 项 deep-dist-import 全部是不合法内部导入**（逐项比对 151 个公开 `SURFACE_MODULES`，命中数 0），涉及 12 个未注册内部模块；其中 3 个有直接可用的公开 Surface 落点（`Foundation/Roles.js`→`RolesSurface`、`Context/Trace/TerminalReporter.js`→`SemanticTraceSurface`、`Repository/Programming/Js/Failure.js`→两个 Failure 面），其余 9 个需先开/确认公开 Surface 或改走真实 Host 路径。30 项 `du-shape` 与深导入同源，迁移时须一并处理。
- **C 类 0 项**：本轮没有词法误报——pyramid 门禁按 AST 判定嵌套深度，boundary 门禁命中的都是真实违规写法。

### 十·四 仍未闭合（需继续核查，非本批范围）

- **B1** —— **已闭合**（2026-10-21 按当前代码核实，非本批引入）。`fork` 与 `commission` 共享 `deriveNameFromCharge`（`Description.fs:429`，`:565-571`、`:575-580`）；`resume` **有意不派生**（`Description.fs:581-585`），符合现行 WHAT（`repository-programming` [056]：`resume` 的 `payload` 必带 `name`，拒绝码含 `name-required`）。旧条目的「resume 应派生」是过时义务。
- **B4** —— **已闭合**（2026-10-21 按当前代码核实，非本批引入）。`ReviewOnly` 子对象的成员读 `this._api`（构造器收到的就是 `api.reviewOnly` 命名空间本身，`Description.fs:771-778`），不再误访问 `this._api.reviewOnly`；缺失成员由 `missingMemberGuard`（`Description.fs:440-448`）以 `{ ok: false, code: "REVIEW_ONLY", reason: ... }` 拒绝。
- **B5** `tryTakeRenderedFor` 只比 `ProviderRun` 不比 `ToolCallId` —— **仍未修**。本批的处置是**删除**了新增的 `tryTakeRenderedFenced`（它无调用方，且 owner 的 `claim` 只支持 `peek`/`cache`/`run` 三种 mode，`run` 只比 providerRun，见 `wanxiangli/src/delivery/Delivery.fs:154-176`），并把 `tryTakeRenderedFor` 的注释改成实话：它只比 provider run、是 harness/fallback 通道、**不是交付证据**，真正的证据是 durable 见证。按最新 WHAT（`repository-programming` [054]/[059]）核对后，**完整 ToolCallId 围栏消费仍是未闭合项**，保留在本任务书待办。
- **B8** S4 终态中断未按当前物理输入设围栏 —— 未核。
- **B9** `PluginTransforms.fs` + `NarrativeTransform.fs` 五项 —— 未核。
- **B10** `requirements/work/tests/011.test.mjs:233` 的 `test.todo` **仍是 todo**；`requirements/work/tests/049.test.mjs` 的中断断言仍需核（空断言）—— 未修。
- **C1** Plan 路径无 settlement —— 未核。
- **C2** post-S3 交付未登记「已成立」 —— **已闭合**（2026-10-21，见「十·二·二」）。`deliverPostS3` 与 `deliverCursor` 现在都登记「已成立」，且登记携带**交付当时绑定**的物理输入（登记时刻不再重读 `CurrentPhysicalUserMessage`）；删除了那个回落到登记时刻重读的 `RegisterPostS3Interrupt` 别名。`work-026` 仍红，但红点已不是登记缺失，而是该用例的断言还挂在工具事件边界上（现行契约把中断放在 transform 裁决边界）——迁移属 B10/work-049 下一轮。
- **C3** `Execution/Fission/OpenCode/Host.fs` lane 交付死代码与跨会话搬运 —— 未核。
- **C4** `requirements/repository-programming/tests/README.md` 条目与新参数名同步 —— 未核。
- **D1** 仍调用退役宿主工具的用例迁移 —— 未做（`work-021`/`work-024`/`intra-participant-parallelism-018` 即属此类）。
- **D2** `delegation-029` 词法审计与 `js-semantic-surface-002` 边界门禁 —— 未核。
- **D3** 补回归用例 —— 仅第一项（三态冲突 + handle 未被消费，`repository-programming/064`）已写；**其余四项仍未写**：交付→见证落盘→transform 停住该 attempt（条款 3/4 的核心闭环）、提交/呈现/中断三件事分别取证、fork 侧重复读取同一终态不掏空交付格、sync delegate 被中断后仍以交付视图结算调用。
