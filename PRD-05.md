# 问题 4：DEBUG 与结构化日志

## 一、Flow 管线位置

本问题映射到管线上的 **effect 执行区之外**。日志不进入 `scan`（不改变状态），也不进入 effect flow（不是 Host I/O）。日志作为独立的诊断 sink，由 Step 中派生的 `LogEntry` 在 scan 外消费。

## 二、当前问题

至少已明确看到 context budget observation 直接向控制台输出：
* provider list 不可用；
* provider.list 调用失败。

这些属于可预期的能力探测和降级，不应默认污染用户控制台。`OpencodeContextBudgetObservation` 当前直接输出 provider list 不可用和 provider.list 失败信息。

## 三、OpenCode v1.17.13 日志契约

### stdout 禁止规则

OpenCode 的 Effect logger 默认走 stderr，但普通插件中的 `console.log` 会写 stdout。

在以下模式下 stdout 可能是协议通道：JSON output、MCP、ACP、subprocess、自动化脚本。

因此万象术生产代码中必须禁止：
* `console.log`
* 裸 `printfn "DEBUG"`
* 非协议 stdout write

### Effect logger

普通 Hook 无法直接调用 OpenCode 内部 Effect logger，因此万象术应维护自己的 logger adapter。

### console.log 性能

"Node.js 中所有 console.log 都会同步阻塞事件循环"是过度概括。其具体行为取决于输出目标（终端/文件/pipe）、Node 版本、平台、stream 实现。日志清理的主要理由应是：污染 Host 协议、破坏机器解析、泄露内部状态或用户内容、造成高频 I/O、缺少 severity/结构/限频、不利于测试和生产观测。

## 四、不要简单全局删除所有 WARNING

应先分类：

### 必须删除或改为 trace

* capability 不存在；
* 缓存未命中；
* 正常 fallback 选择；
* 正常 nudge 跳过；
* provider list 不支持；
* 某 optional API 不可用。

### 应改为 warn

* 本应有 model limit，但解析失败；
* 事件日志写入失败；
* schema 完整性检查失败；
* 状态 invariant 被破坏；
* 使用降级 token 估算。

### 应保留为 error

* 数据可能丢失；
* 下游工具执行状态未知；
* event log 与内存状态不一致；
* 用户 Esc 后仍检测到旧 continuation 被发送。

## 五、统一结构化日志

### 5.1 推荐日志出口

* trace/debug：独立文件；
* warn/error：stderr；
* CLI 明确用户结果：stdout；
* event log：NDJSON 专用文件；
* 不把调试日志混入 event sourcing 日志。

### 5.2 日志结构

每条日志至少包含：subsystem、event name、severity、timestamp、sessionID hash、messageID、humanTurnID、continuationID、contextGeneration、hostVersion、degradedReason。

### 5.3 禁止记录

* 完整 prompt；
* 完整工具参数；
* 用户文件内容；
* API key；
* token；
* 私密路径；
* review task 全文；
* 模型隐藏推理；
* 完整 stack 中的敏感路径。

### 5.4 不建议删除所有 Semble trace 调用

若 trace 已经有环境变量门禁、写入独立 sink、默认关闭、不泄露敏感数据，则可以保留。应该删除的是"绕过统一 logger 的裸输出"，而不是删除所有诊断能力。

## 六、输出策略

* 默认：只显示 warn/error。
* debug：显式配置开启。
* trace：只写文件或诊断 sink。
* TUI/插件运行时不得直接 `printfn` 或 `console.log`。
* 相同错误需要去重或限频，避免 provider API 每轮失败都刷屏。
* session 结束时可以输出一条汇总，而不是几十条过程日志。
* OpenCode 本身没有日志 rate limit，万象术需自行做错误去重和限频。

## 七、CI 静态门禁

生产模块新增以下内容时构建失败：
* `console.log`
* `printfn "DEBUG`
* 无 logger 包装的 stderr write
* 直接输出完整 prompt/args

测试模块和明确的 CLI 用户输出需要列入白名单。

## 八、验收

自动化测试启动完整插件并执行普通会话：
* stdout 不包含 `DEBUG:`；
* stderr 不包含普通能力探测；
* debug=false 时没有 debug 级日志；
* debug=true 时结构化字段完整；
* 日志中不含 prompt 和控制字段原值；
* 默认运行 stdout 中 `DEBUG:` 数量为 0；
* 敏感 prompt、工具参数不进入日志。
