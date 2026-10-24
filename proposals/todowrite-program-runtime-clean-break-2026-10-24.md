# todowrite：命名程序、顶层 planB 与 program/runtime clean-break

> 日期：2026-10-24。状态：实现与范围内验收已完成；精确版本与未通过项见第 10 节。
> 本文记录已批准方案及实施期间的修订：planB 从程序成员移到工具顶层；一次请求只建立一个期限，约束其全部 call、unpark 和恢复目标。
> 构建、测试、干净安装、提交与运行部署分别记账，不互相代替。

## 1. 公开入口

`todowrite-for-ROLE` 恰好接收三个必填字符串：

```ts
{
  name: string;
  program: string;
  planB: string;
}
```

name 与 program 不得为空白，planB 不得为空字符串。模型为每个新运行明确取名；宿主不按物理调用身份偷偷生成名字。程序保持 `class Js extends JsProgram` 和 `async run()`。旧顶层 unpark、abandon、handle、prefixUntil、carry 不构成另一条兼容入口。

| 成员 | 合同 |
| --- | --- |
| `this.call(command)` | 排空此前 SDK 操作，然后启动一条 shell 命令，阻塞所属程序。 |
| `this.park(payload)` | 显式暂停，恢复值成为该表达式的结果。 |
| `this.unpark(name, value?)` | 恢复同会话内的原程序，或返回其保留终态。 |
| `this.abandon(name)` | 请求取消并确认资源收尾，不是删除登记。 |
| `this.signal(name, signal)` | 控制目标当前唯一命令，不恢复程序。 |
| `this.send(name, input)` | 向目标当前唯一命令提交一行输入，不恢复程序。 |

call 只能传一个参数；unpark 最多两个，第二个仅是显式 park 的恢复值。成员不能接收或创造预算。`const planB = 'x'.repeat(48000)` 只是程序局部变量；将它作为 call 的第二参数或 unpark 的第三参数会在副作用前被拒绝。

文件、委派、评审与其他成员仍按唯一角色能力来源投影。Manager reviewOnly、Plan 写前评审和 PLAN_PATH、Ask 的既有业务准入保持。委派 `resume(payload)` 不等于程序恢复；普通 return 不等于业务 deliver。

## 2. 一次准入，一条绝对期限

工具完成参数及权限准入时一次计算：

```text
budgetMs = UTF8Bytes(planB) × 50
deadline = admissionTime + budgetMs
```

按 UTF-8 字节数计价，每 20 字节为 1 秒。planB 是工具参数中的文字，不作为代码执行。wanxiangli 不截短合法预算，不另加隐藏短上限。

本次所有操作、SDK 排空、连续命令及 unpark 唤醒的目标共享同一个 RequestWindow。目标继续唤醒其他目标时仍使用该窗口。任何 call、命令退出或嵌套恢复都不能刷新、叠加或延长期限。

| 计量 | 等待期间的行为 |
| --- | --- |
| 程序计算预算 | 等待时停表，恢复计算后使用剩余量，不重新发满。 |
| 请求期限 | 持续推进；到期暂停活动程序并返回真实暂停回执。 |
| 命令运行时长 | 可观察；不是由 planB 暗中设定的命令寿命。 |

底层 ProcessSession 的显式物理有界执行合同继续存在。它的运行硬顶与模型请求期限不同，不能因迁移而取消。

## 3. call：先等真实结清，再启动唯一命令

进入 call 时先检查形状和权限，再等待本程序此前已经发起的 SDK 操作取得真实终态。仍在途不是错误，不报 PENDING_OPERATIONS，不要求模型补 await 后重发，也不取消原操作。

真实 SDK 异常沿原错误合同呈现，已落盘效果保留；业务成员返回的 `{ ok: false, ... }` 仍是该成员的业务数据，不伪造成执行器异常。排空完成后才启动命令；排空与命令等待使用同一剩余期限。

每个命名程序拥有独立执行进程。同步桥接在 call 表达式求值处停住执行单元，漏写 await 或写入 Promise.all 都不能越过首个 call 启动第二条命令。宿主、其他程序、既有 SDK 与输出处理继续响应。普通 Promise、忙标志、隐式命令队列或整段工具调用的全局锁不能替代这一保证。

并行命令通过不同名字的程序表达。shell 自己组织的子任务遵守真实 shell 语义，不靠审查命令内容实施单 call。

```json
{
  "name": "build",
  "planB": "等待本次构建；窗口到期时保留原命令与程序现场，之后继续领取结果。",
  "program": "class Js extends JsProgram { async run() { return await this.call('node scripts/build.mjs'); } }"
}
```

## 4. 暂停与恢复

| 窗口到期时的事实 | 程序与命令的后果 |
| --- | --- |
| SDK 尚未结清 | 程序停在排空点，命令未启动。SDK 后来结清也不自行启动命令。 |
| 命令仍运行 | 程序停在原 call 点，命令可以继续，输出继续采集。 |
| 暂停后命令退出 | 保存真实 CallResult，续体仍暂停。 |
| 新请求显式 unpark | 原现场加入新请求窗口，接着排空、等待或领取已保存结果。 |

新控制程序在顶层提供自己的 planB：

```json
{
  "name": "continue-build",
  "planB": "继续等待原构建；如果本次期限用尽，保留相同现场和新增输出。",
  "program": "class Js extends JsProgram { async run() { return await this.unpark('build'); } }"
}
```

控制者及本次唤醒的目标共享新窗口，到期时仍活动的各方实际暂停。先恢复控制者，只能推进控制者自身；它此前收到的目标暂停回执保留原样，不会隐式再次唤醒目标。继续目标必须另有显式 unpark。

只有显式 park（包括 where 歧义）接收 value。call 必须取得原命令真实退出、信号和输出，恢复参数不能冒充 CallResult。控制程序的成功不覆盖目标 name、state、effects 或失败事实。

## 5. 名称、身份与重放

ProgramSession 按所属会话登记名字，一个名字对应一次运行。相同物理工具调用及相同参数重放原回执，不执行第二次；另一新调用占用已有名字报 NAME_TAKEN。终态保留到会话清理，新运行使用新名字。

恢复直接定位运行对象，不重新编译、不重建沙箱、不扫描工作区、不重发命令。暂停代际、命令代际及调用围栏私有维护，重复或迟到消息不能消费下一代现场。

进程重启不恢复丢失的 JS 栈。恢复只使用实际持久化的归属与终态；没有现场时报告中断或未知，不能以名字存在为由自动重跑已可能产生效果的源码。

## 6. 控制、清理、输出与结算

signal/send 只作用于同会话目标当前唯一活动命令。没有活动命令，包括仍在排空时，明确报告事实，不猜最近进程、不把输入留给未来命令。控制不解除 park；发送信号不等于物理退出，输入提交不等于对方已处理成功。

abandon、会话关闭与所属插件退出复用收尾。插件只关闭本实例拥有的会话，程序收尾先于执行端口和 Journal 释放，不使用全局 closeAll。先取消并清理，确认执行进程、shell 与受管进程组结清后才报告终态；无法确认时保留收尾状态和错误，不释放旧资源再启动第二份。正常展开可运行原生 finally，强杀不保证 finally，已落盘效果不回滚。

每个 Run 终态的 effects 经 settle 一次结算并保留，重复控制不重结。目标 effects 留在目标回执，不冒充控制者新产生的文件效果。持久错误保留身份和原因；生命周期审计按序等待，失败以 lifecycleError 进入回执，不抹去真实暂停或已发生效果。

parked.delta 是新增观察，不消费最终 CallResult。退出码、信号、完整输出文件、字节数及截断分别保留；信号退出的 null 退出码不得补为零。原始输出文件在程序和会话关闭后保留，没有按保留期自动 GC。

源码沿既有渲染边界完整呈现；能可靠定位时标记实际暂停行，不能定位时降级。新状态以新回执呈现，不回写旧工具结果或历史 Pair Hint 字节，不另造会失配的健康账本。

## 7. 独立 join 与 DAG 退出

独立 `join()` 无参数、无 name/program/planB，不建立临时 JS 程序。它沿既有工作归属、结果批次、准入与中断规则收拢，不证明全部子工作成功。用户输入或宿主中断只结束这次等待，不顺手取消子工作，不把超时呈现为完成。

编程内 join/dag 的能力投影、生成基类、绑定、资源、示例与专用隐藏执行路径退出。依赖由模型显式安排：程序派工，独立 join 收拢，再提交下一程序。独立工具保持原有散文与结构材料边界，不保留已退役的程序结果包装。

## 8. 两包的实际所有权

| 入口 | 所有者与职责 |
| --- | --- |
| `wanxiangli/program` | Program() 登记入口；ProgramSession 会话内名称、重放与协作；私有 Run 单次运行、窗口、恢复代际和结算；ProgramCatalog 成员投影；Wait 等待控制。 |
| `wanxiangli/runtime` | Runtime 独立进程、隔离与通信；RuntimeTools 原语和字面锚点；FileSession 读快照与版本；Filesystem、ProcessSession、Terminal、OutputSpool、Time 各自物理资源。 |

program → runtime 是明确的永久单向基础依赖，仅消费公开产物。runtime 不解释 planB，不认识角色、评审或业务 deliver。两包不声明彼此独立，不跨源码编译，不保留转发聚合壳；库根规则已明确这一适用边界。

| 撤销入口 | 当前实现归属 |
| --- | --- |
| programming | program/Catalog |
| sandbox | runtime 独立执行；生命周期归 program，旧 sandbox 删除 |
| fs | runtime/files 与 FileSession、RuntimeTools |
| process / pty / spool | runtime/process、pty、spool |
| time | runtime/time；预算语义归 program |
| wait | program/wait |

全部八个旧 exports、旧工程与被替代 owner 退出，包构建清理过时 dist 目录，干净安装不包含旧八目录或 F# 源码。Work、Journal、权限等其他独立领域保持各自归属。宿主只经 npm 公开入口消费；HNI/OpenCode/OMP 的物理编码与业务准入不进入程序设施。

## 9. localspace-js backport

相同思想已回移至 localspace-js：`js({ workspaceId?, program, planB })`，`call(command)`，`unpark(handle,value?)`；planB 只在请求准入时计算，同请求的命令、恢复与嵌套目标共享期限。

localspace-js 当前公开上限为 `min(UTF8Bytes(planB) × 50ms, 30000ms)`。这是该仓已有并发改动确定的等待合同，不回灌成 wanxiangli 的隐藏上限。断开连接保持 detach 语义，不续期、不杀原命令；仅显式新请求可恢复。

这次 backport 约束 broker 的 call/unpark 准入与回复交付，保留 localspace-js 原有后台 worker 语义：直接在 worker 内执行的计算和文件 API 不因等待到期而被整体冻结。不能把它写成“期限到达后所有代码和文件副作用都停止”。

## 10. 当前验证与交付边界

| 层级 | 已取得的证据 |
| --- | --- |
| 程序与运行时行为 | 当前 64 项测试分别在 Node 26.12.0 和 Bun 1.4.2 通过；覆盖真实 SDK 排空、未 await 阻塞、共享到期、控制、收尾、输出和隔离。 |
| 宿主核心回归 | repository-programming、process-execution、capability-enforcement 共 641 项：622 通过、0 失败、12 跳过、7 TODO；包含新增的真实命令退出、finally 落盘和跨插件隔离反例。跳过和 TODO 不计作通过。 |
| 干净安装 | program/runtime 新包的 Node、Bun 实际消费及严格 TypeScript 检查通过；旧入口不可导入。 |
| 库全量基线 | 最终全量 947 项：931 通过、16 失败；全部失败标题与原提交 f553f3ed07c4c0c6d5042d2d8561e66483a1b600 隔离构建的 16 项 Assembly `Missing field: updates` 逐项一致，没有新增失败。 |
| localspace-js | 最终源码重新执行完整 npm test、typecheck 与临时目录生产构建，全部退出成功；收尾复核发现并修复的重复 RequestWindow 测试导入已提交为 `78e83c4`。包含 8 项共享预算回归，保留 30 秒上限与断连 detach 行为。 |
| 其他消费者 | 工作、委派、问答、完整 managed-session-lifecycle、Bookkeeper 等共 614 项：564 通过、1 项既有失败、5 跳过、44 TODO。剩余 work-011 温度断言来自未修改的 PluginHooks/ChatParamsHook：两参数调用没有进入柯里化函数的第二层；独立用例和实际编译产物均已确认。 |
| 构建与源码门禁 | 项目规定的 Fable 构建通过；本任务 88 个 F# 文件格式检查通过，JavaScript 语法检查通过。全仓格式仍有 53 个未修改文件不满足格式，静态检查剩余 25 项位于未修改的 Host/FissionPort、OMP、SecondHost、PluginTransforms。 |
| 提交 | wanxiangli `b5187fc07d3da16ed14ad097c5bd0c01ecde822d` 已推送 main，宿主本提交以 gitlink 固定该版本；localspace-js 的共享窗口实现为 `42da3a896f1ac322dd7622cf00ffe07779563b99`，参数文档补全为 `03991b87882e3d0974fc95927601eba2ce531a21`，测试导入修复为 `78e83c4073c0950a7b1b091e360c673f1fd7a9ee`，均已在 main。宿主实现 `5e22533250926d82dee34470913ebf18156b2801` 已推送 master，后续验收记录提交不改变运行语义。 |

上述宿主和库回归对应本次实际工作树，其中保留了开始任务前已有的并发修改；这些修改没有纳入本任务提交。干净安装仅证明表中列明的公开包消费范围，不能将工作树结果改称为提交在干净 checkout 中的全仓验证。

本记录不宣称整个仓库所有历史缺口已闭合，不据 Linux 的 Node/Bun 结果宣称 Windows、macOS 或真实模型宿主通过。此次没有主动重启、部署运行服务；源码提交与正在运行进程加载哪个版本分别核对。

正式合同见 repository-programming/WHAT.md、process-execution/WHAT.md 与各业务 WHAT。实际所有权、两仓提交、最终门禁及剩余边界统一登记在根 AGENTS.md 的迁移章节。
