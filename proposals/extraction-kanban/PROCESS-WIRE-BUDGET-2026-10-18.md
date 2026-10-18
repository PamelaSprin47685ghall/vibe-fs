# CUT-02-R6-B / GAP-092：完整结果的字节预算

输入主仓`ebfbe3d83`，固定库`df14f48 / 0.6.8`。R7-B交付后认领本包，R12-U1继续暂缓。用户允许不兼容调整并授权本轮非作者复审。本包修改工具呈现、runner预先取消准入和真实消费者接线，不改库或引入第二份进程状态。

## 契约与实现

`output_budget_bytes`约束最终Provider结果的UTF-8字节，包括程序事实、TOML结构、转义、截断说明及原文。原文留尾是另一个小容量算法；1/3/4/7等预算只用于验证该算法，不再作为可执行工具请求。必要结果的最小预算按当前语言的真实格式推导，覆盖最宽退出码、空尾截断及所有现有固定失败/权限/恢复说明。额度不足时在执行前返回`executed=false`、`rejected=output_budget_too_small`及最小字节数；该明确拒绝不是执行结果，不受无效零预算约束。

Completed与Spooled都先检测完整编码；不合限时，在最新原文尾部中探测候选，实际生成每个完整报文并计量，只保留证实能容纳的候选。保留尾部字节和UTF-8边界，不选择“重要行”或调用模型，不声称TOML表示切换下必为绝对最长尾部。不能用raw字节数代替编码后字节数。

真实Host还有2000行/51200字节窗口。复审证实只修ExecutorTool不足：统一注册层会再次裁尾、加marker，丢掉exit_code并破坏TOML。现使用Host唯一`ToolResultBound.fitsInWindow`，与请求预算共同约束Completed、Spooled和每个尾部候选；Spool预读也不超过实际Host字节窗。注册层和Host继续拥有自己的规则，run交出的完整结果使它们保持identity，不改统一裁切器或按工具名绕过。大额度不能扩大实际Host窗口。

ProcessOutput拥有唯一的文本解码，使用ignoreBOM=true保留字面的U+FEFF，工具尾部共用它。预先取消的请求在ProcessRunner入场时明确返回取消事实、零启动；取消文案改成“已请求取消，尚未确认执行已结束”，不伪造完成。

## 问题分类及反证

| 分类 | 事实与处置 |
|---|---|
| 修前已知缺口 | 原GAP-092仅限制raw tail，256字节请求返回549字节。原测试去TODO后确切失败；不归因于本轮。 |
| 修前新确认 | 小输出解码吞BOM；Host注册层二次裁切；已取消请求误报SpawnFailed。分别取得真实反例后修正，不以局部Surface绿结果代替最终消费者。 |
| 本轮新增实现错误 | 初版按wire超额数直接扣raw容量，控制字符和引号尾部被过度删空。新增非空反例再红2项，改成完整候选探测后验收。最初使用Fable不支持的GetByteCount导致编译失败，已换受支持的UTF-8编码计数，没有运行dotnet build。 |
| 测试前提与review纠正 | Bun目标process.stdout.write后立即process.exit，会在无库的原生对照中也丢未刷输出；改用同步写者。新注册插件夹具须在启动Bun前准备独立HOME，不能在进程内改HOME后假定生效。并发大组时Bun PATH例超5秒，原失败及后续污染保留；不加timeout或删断言，以单独运行复验。过早在失败构建期间启动的测试不算候选验收。 |
| 仍未修的旧协议问题 | 原生code143与SIGTERM在NodeProcessHost被归一为同一码，预算前后及Node/Bun均复现。单列R6-D，不能随本包关闭WHAT010/014全部义务；见下文。 |

初始八项实际1pass/7fail，`/private/tmp/process-wire-budget-red.log`；包括549/256、两语种零预算、转义、多字节及BOM。初版空尾错误为`process-wire-budget-candidate-first.log`，不因文件名含green就当通过。旧直接测试的小预算改为raw算法测试；真实工具测试仍执行物理程序并逐字节检查最终报文。

## 非作者与消费者证据

`review_guidance_r3`确认原1996LF/2039字节报文经真实register变2045字节且丢事实，`process-wire-budget-review-register.log`。636候选同步写者的Node/Bun各四项边界转绿：固定1996LF/2039、2000LF/2043、2500LF及70k字符，注册恒identity、exit7和原始suffix保留。日志`process-wire-budget-review-register-fixed-sync-{node,bun}.log`。同候选每Host各16项双语观察覆盖exact-minimum空成功、上下文拒绝、真实SpawnFailed、timeout、恢复阻塞及BOM/control的Spool尾，`process-wire-budget-review-probe-636-{node,bun}.log`。这些是636范围证据，最终取消前置和精确包仍须单列。

`review_process_a`独立核对真实stdout/stderr/BOM-only、非法UTF-8既有替换语义、控制字符和大量引号报文；完整含转义报文恰51200字节时真实register不改。证据`/private/tmp/gap092-independent-evidence-j1A9wt/summary.json`；其中36次真实目标执行含24次旧新事实链对照和12次当前编码/注册观察，不冒称36项测试套件。明确借用了主仓其他声明依赖，不称完全独立安装。

实际注册Plugin的DevOps run已在合法authority下执行拒绝、256字节大输出和行超限，均合法完整且零模型会话创建；前期探针`/private/tmp/process-wire-registered-plugin.log`。正式013已将原TODO替换为真实注册工具的拒绝、小/大/行超限及模型创建/发送计数检查，随下列完整组通过。Surface与真实注册层各自验收；没有外部LLM调用。

构建639，174 surfaces/839 modules。相关129文件955项：911pass/0普通fail/15skip/29TODO，`/private/tmp/process-wire-budget-related-final.log`。Bun预先准备独立HOME的011/013/014/015完整46/46，`process-wire-budget-bun-home.log`；这是物理原生证据，不冒称正式Bun监督。正式Node九文件96pass/0fail/1integration skip，9/9完成、owned accepted=true，`process-wire-budget-formal-final.log`；另实际Fable正反编译1/1、真实OpenCode Host1/1。format和static通过，既有report-only提示不算失败或新增豁免。

固定主包`/private/tmp/wanxiangshu-budget-b1f7804.tgz`，SHA-256 `b1f7804e9a3a75fce9d801c7e018cb1ed8085387343a97794425e815df497506`，2076 members/展开13402186 bytes，内含唯一库0.6.8。默认verify-package独立消费者通过。非作者同一包Node/Bun各18项观察覆盖双语exact minimum、低预算/预取消零Spawn正反控、注册窗口、内存/Spool BOM；2076个产品文件前后摘要不变，库ESM确实落包内。证据`/private/tmp/process-wire-budget-review-exact.cxnJg3/`；借用八个声明依赖及smol oracle已明示，不称完全独立安装。初版探针误用CommonJS resolver读取import-only库，业务断言已过但证据脚本失败；改ESM后才记完成。

扩展仓外夹具先被runtime-root同名package自引用误导，又漏装声明的Host peer，两次失败分别保留`process-wire-budget-isolated.log`、`process-wire-budget-isolated-final.log`及内层日志；只修自有夹具，未修改产品包。补独立npm安装声明peer后的最终结果：Node/Bun四文件各46/46、Bun128MiB逐字节/hash五次均1/1、完整PTY025为124/124。证据`/private/tmp/process-wire-budget-isolated-peer-final.log`及`/private/tmp/process-wire-main-package-evidence-Hu0Aqi/`；2076个安装产品文件与归档前后相同、只使用包内库、运行目录已清理。普通运行依赖和Host peer独立安装，smol-toml1.8.0作为测试oracle从主仓复制，requirements/scripts是夹具而非替换产品源码。原始失败均保留，不将中间候选与最终产物重复加总。未部署用户Host，未发布registry。

## 新确认P1：CUT-02-R6-E，取消监听与排队准入

非作者在固定b1f7804包的Node/Bun均确认：真实AbortController先add随即remove listener，LargeGate被占时取消未收束，释放许可后仍执行`printf started-after-abort`。`ebfbe3d83`已有同一代码；这是修前问题，不是预算回退。根因ExecutorTool的try/finally只包住Task创建，没有包住await。证据同上述目录`late-abort-{node,bun}.log`。本包只关闭预取消，不泛称中途取消已修。

下一小包将finally移入实际等待的task作用域，并将LargeGate排队取消与运行中已有取消异常映射为ProcessCancelled；保留waitForExit独立API的reject/kill语义，不伪造物理终态或改变signal协议。验收需真实listener、排队取消零启动、取消后继任正常准入、运行中物理取消、失败/成功解绑及Node/Bun精确包对照。

## 新确认P1：CUT-02-R6-D，保留确切终结事实

非作者四腿旧0d60精确包/当前635×Node/Bun均exit0：同一真实Node目标正常退出143时库为`{code:143,signal:null}`；自发SIGTERM时库为`{code:null,signal:SIGTERM}`。主仓ProcessSurface、Executor及注册后报文却都变成143且完全相同。丢失点NodeProcessHost原有reportClose；本包没有修改它。归档、已选产品摘要不变，临时运行目录清理；证据同上述独立summary。

下一包原子调整纯事实契约：用`Exited(code)`/`Signaled(nativeName)`保留原生区别，接NodeProcessHost、Wait、Runner及内存/Spool结果，再调整工具与Git真实消费者；删除128+signal归一和旧期望，不给旧int附加模糊字段。取消/超时请求与物理终结仍分开，缺回执保留Unknown；不猜signal、不造-1退出或重发。重算双语最小wire预算，验证高退出码、各本机signal、早关管道、取消/超时及精确包后才关闭该范围。跨重启自然Unknown R12-U1不在此包追查。
