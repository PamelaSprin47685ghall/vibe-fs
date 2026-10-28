# repository-programming 测试说明

本目录测试套件验证 JS 编程面的行为：能力生成、沙箱隔离、文件快照与修改、生命周期与持久化。这里说明用例的覆盖点与已知边界，供编写和维护测试的人使用。

## 用例覆盖导引

- **001—005、021—022**：生成器输出与调用准入。核对各角色生成的工具名、Schema、基类与示例与实际授权能力一致；非当前 Attempt 或跨角色的工具名被拒绝；描述不泄露宿主内部键。
- **006**：执行进程隔离与计算预算。核对在独立 `node:vm` context 中执行，全局只有获授权成员；同步循环受 VM 超时保护，输出超出 UTF-8 字节上限报 `RESULT_TOO_LARGE`；constructor 逃逸被拒绝。
- **007**：不可变快照。`cat` 读取的内容后续不随 `write` 改变，非法 UTF-8 报 `INVALID_UTF8`。
- **008—016、018—020、023—024**：主文件原语与事务行为。核对 `cat` + `where`/`whereAll` + `slice` + `write` 的组合写法；Write-only 表面不暴露 `cat`；`where` 歧义/未命中产生包含 candidates/nearest 的 parked 载荷；`write` as-is 落盘（非法返回值后已落盘改动仍保留）；`mv`/`rm` 的 POSIX 语义与稳定失败码。
- **017**：同会话并发调用的确定性串行化与读快照隔离，后提交的事务能读到先提交的效果。
- **025**：坏输入与致命错误时的语义熔断。验证先完成 durable cut-tail 结算再触发熔断，保留已落盘的改动。
- **026**：实质访问分离。`runObserved` 与 `AccessTracker` 只把显式 `cat` 与已提交的修改/创建/删除/移动记入案例关联，写前快照与 `ls`/`stat` 内部扫描不进入关联。
- **027**：Engineer 与 DevOps 相同能力生成相同成员，统一通过生成的编程工具访问文件系统；Manager 仅通过 `reviewOnly` 访问仓库只读原语。
- **028—029**：程序执行句柄与逐笔落盘。运行期 deadline 计量与超时拒绝；单笔写入即时落盘与写前 `FILE_CHANGED` 校验。
- **030—037、039**：park / unpark / abandon 全周期。parked 载荷结构与 TOML 渲染；同一名字不能被活动程序重复占用（`NAME_TAKEN`）；终态后名字可复用；`abandon` 触发 finally 并结清资源；放弃期间禁止再次 park（`CANNOT_PARK_WHILE_ABANDONING`）；名字按会话隔离且无数量上限；两包边界（`wanxiangli/program` 拥有状态，主仓通过 Programs.fs 组装）。
- **040**：rm/mv 的 POSIX 语义、越界解析与 durable 单笔记账。
- **041**：生产接线。`IJsLifecyclePersistence` 沿 PluginHostInterop → ToolRegistry.create → JsToolSpec 与 PluginBoot → PluginRuntimeScope → PluginSessionScope 贯通，四类事件写入同一审计流。
- **042**：rm/mv 落盘后程序失败的清单形态。失败结果的 `[fs]` 包含实际已落盘的 removed/moved 条目。
- **043—044**：双语资源成对一致性（zh-CN.md 与 en.md 占位符相同）；稳定失败码与包含路径的 reason 对应。
- **045—047**：文件原语行为与组合模式。cat/ls/stat/mkdir 的成功与稳定失败形态；where 字面 IndexOf 规范化；whereAll 起始偏移与全文匹配；原语组合食谱。
- **048**：挂起源码标记。完整程序回传，当前挂起行以 `>| ` 标记，其余行以 ` | ` 呈现；`eval` 求值内的 park 标记在程序自身的 `await eval` 行。
- **049、064**：公开工具入口签名。工具接受 name、program、intent 与 remainingScopeTurnsEstimate；缺项或类型不合在启动前拒绝；无顶层 unpark/abandon 参数。
- **050—051**：命令执行与控制。`call(command)` 的排空屏障与独占执行；`signal(name, signal)` 与 `send(name, input)` 从控制程序向目标活动命令派发；单参形式报 `INVALID_ARGUMENT`；目标无命令报 `CALL_CHILD_UNKNOWN`。
- **052**：准入期限与执行预算。按 intent 的 UTF-8 字节数计算期限；SDK 排空与命令等待不停表但暂停扣减执行预算。
- **055**：SDK 并行与 call 屏障。同一程序内 `Promise.all` 运行多个 SDK 操作各自结算；进入 `call` 时先排空既有操作，同一程序不支持两条命令并发。
- **056**：委托原语数据信封。fork/resume/commission/devops/fission/horizon 返回数据信封；未安装宿主返回 `DELEGATION_UNAVAILABLE`；`join()` 宿主工具收拢子智能体终态（含 voided 分支）；顾问（advisor）不作为编程面成员。
- **057**：`ls` 的 gitignore 语义（逐级 `.gitignore`、否定规则、`.git/info/exclude`）与递归组合。
- **058**：按名字控制命令与无隐式目标。单参控制被拒绝，目标缺失明确报错。
- **060**：机器词汇与流名持久性。`js-tools/transactions` 与 `js-tools/lifecycle-audit` 流名稳定；结算失败不影响已落盘事实。
- **065**：并行派发。同一会话的不同命名程序可并发执行，等待中的程序不阻塞其他程序或独立 join。

## 运行方式与注意事项

- 测试使用 `node --test` 驱动，位于各用例文件中（`001.test.mjs` 等）。
- 依赖构建产物：部分用例通过 `dist/` 引入（`WorkflowSurface.js`、`RuntimeSurface.js` 等），运行前须确保构建产物为最新。
- 测试辅助支持：`support/named-program.fixture.mjs` 提供带会话清理的命名程序夹具；`support/command-program.fixture.mjs` 提供命令执行相关夹具。
- 局限：部分测试依赖本地 `rg` 命令与系统 POSIX 工具；跨设备移动（EXDEV）只验证目录登记与原子语义，未在物理不同挂载点运行。
