# repository-programming — README

本包的条款正文在 WHAT.md，用例覆盖与局限在 tests/README.md。这里补说 JS 编程面的操作细节，说法与 WHAT.md 同一套。给谁用：写 `todowrite-for-*` 程序的模型与教它的人，以及排查程序面行为的人。

## 文件原语与组合

canonical 文件成员序为 `write, rm, mv, cat, ls, rg, stat, mkdir, whereAll, where`，只列当前角色实际授权的成员。编辑的标准组合：`cat(path)` 取不可变快照 → `where`/`whereAll` 定位 UTF-16 span → `text.slice` 重组 → `write(path, content)` 一次落盘。`where` 零个或多个匹配时程序挂起（parked 载荷带 `candidates`/`nearest`），用新命名程序的 `this.unpark(name, span)` 恢复。`rg(argv)` 做递归枚举与内容搜索，返回 JSON 对象（内容搜索 `{ exitCode, messages }`，`--files`/`-l`/`--count` 等摘要模式 `{ exitCode, paths }`）。写法示例见双语资源 `tool/js-program/recipes` 与 `tool/js-program/run-recipes`。

## park / unpark / abandon 生命周期

每个基类都有 `park(payload)`、`unpark(name, value?)`、`abandon(name)`。名字按会话登记（`NAME_TAKEN` 拒绝占用中的名字，终态后名字可复用）；没有数量上限，也没有 handle——恢复与放弃都按名字。`unpark` 的 value 只交给显式 park（含 where 歧义）；`call` 的结果由目标程序自己的回执携带。`abandon` 触发原生 finally，finalizer 内的 rm/write 照常 as-is 落盘；放弃期间再 park 报 `CANNOT_PARK_WHILE_ABANDONING`；捕获了取消的继续执行，其返回值被丢弃，结果仍为 Abandoned。

## 交付前的未结清检查

存在未处理 parked 程序时交付被拒并列出全部名字（资源 `tool/js-program/parked-deliver-blocked`）。每回合提醒经 PairHint 的 composeMarkerDocument 并入（资源 `tool/js-program/parked-hint`），无载体回合不注入。会话关闭在 ClearSession 清理链收尾该 session 名下全部程序并记 Reaped 审计。两个资源均为 zh-CN.md/en.md 成对双语、占位符一致且含 `{{handles}}`。

## rm / mv 编程面

`rm`/`mv` 由 Remove/Move capability 在 js 编程面上投影，与 `write` 同属 as-is 逐笔落盘路径：rm 删文件或空目录（非空报 `DIRECTORY_NOT_EMPTY`），mv 按 POSIX 语义移动或重命名并支持覆盖（跨设备报 `CROSS_DEVICE_MOVE`）。路径按字面解析（相对路径接工作区根、绝对路径原样）。成功渲染报 `removed = [...]` 与 `moved = ["from -> to"]`。每次 rm/mv 各记一笔 durable 条目，Prepared/Committed 成对排空 pending 投影。

## call / signal / send

Exec capability 投影 `this.call(command)`、`this.signal(name, signal)`、`this.send(name, input)`。call 只接受一条 shell 字符串；先结清本程序既有 SDK 操作再启动命令，排空与等待共用入口剩余期限，到期不杀命令。signal/send 从新命名程序作用于目标当前唯一活动命令；没有活动命令报 `CALL_CHILD_UNKNOWN`（名字未知报 `PROGRAM_UNKNOWN`）；单参形式报 `INVALID_ARGUMENT`。send 为缺行尾换行的输入补一个。命令的最终结果（退出码/信号、完整输出、截断、字节数、时长）不被增量 delta 消费。

## 委托原语：fork / resume / commission / devops / fission / horizon / join

**所有 js.* 成员返回数据信封，不返回渲染后的 prose**（宿主在工具结果与 parked 边界统一渲染）：`{ ok: true, ... }` / `{ ok: false, code }`。

- `fork(payload)`：字符串或 `{ charge, name?, calling?, keywords? }`（缺名由 charge 派生），返回 `{ ok, name, agentId, outcome }`。
- `resume(payload)`：带 `name` 与本次 `charge` 续做既有道路；目标正忙时追加忙碌提示（`outcome` 为 `nudged`）。
- `commission(payload)`：与 fork 同形，委任既有角色。
- `devops(intent)`：请求本 session 绑定的 DevOps 席位；未绑定或已关闭报 `assessment-pending-for-devops`。
- `fission({ charge, lanes })`：同身份多 lane 分片，返回 `{ ok, groupId, lanes }`；owner 的 `join()` 收拢本组。
- `horizon()`：在场名册数据信封（每人 status/workState/record 与四个校准数）。
- `join()`：独立宿主工具（无参数，不创建 JS 程序），收拢本会话成员，`status ∈ completed | failed | abandoned | voided`。
- 顾问（advisor）不是可调用成员：由 Scope 进度估计自动触发（见 advisor 包）。

宿主经 ActiveTurnBridge 在每次编程调用期间安装 `JsDelegationHost`（Delegate/Fission/Horizon/Review），调用结束释放；未安装宿主时成员返回 `{ ok:false, code:"DELEGATION_UNAVAILABLE" }`。Orchestrator 无文件权限但持有 Fork/Join，因此获得仅委托的编程面（Fork 投影为 `commission`）。双语教学在 `tool/js-program/delegation-rules`。

## 持久化机制

as-is 逐笔落盘：每笔 `write`（及 `rm`/`mv`）调用即原子落盘，跨笔不回滚；程序以任何方式终止时，已落盘改动保持为事实分界线，失败结果如实附已落盘清单（`# failed` 加 `[fs]`），无落盘改动时不附 fs 节。

durable 记账只进统一 EventStore 流 `js-tools/transactions`：Prepared/Committed 成对追加。记账追加失败以稳定 `TRANSACTION_PREPARE_*`/`TRANSACTION_COMMIT_*` 码拒绝成功、如实上报已落盘效果，不重试追加、不换事务身份，失败携带原事件 id、事务 id 与原始 cause。物理写失败以 `TRANSACTION_COMMIT_FAILED` 失败且失败笔之前的落盘改动保留。Prepare 无 Committed 是中断事实，EventStore 重开只观察记录，不重放、不回滚、不补交。EventStore 端口的坏输入先结算字节事实，再经语义熔断（`js-transaction-semantic-cut`）终止进程。

## Lifecycle 审计接线

Parked/Unparked/Abandoned/Reaped 审计沿生产组装路径接线：PluginHostInterop 从 workspace 的统一 WorkspaceEventStore 同源构造 `IJsLifecyclePersistence`，经 ToolRegistry.create 贯通到每个生成的 JsToolSpec，执行调用点传入该 persistence；执行链记录四类事件到 `js-tools/lifecycle-audit` 流。会话关闭链经 PluginBoot → PluginRuntimeScope → PluginSessionScope 贯通；ClearSession 先枚举名字再收尾，逐名字记录 Reaped。程序状态本身归 `wanxiangli/program`（主仓 `Repository/Programming/Js/Programs.fs` 只做组装），审计失败如实记录，不重跑原效果。
