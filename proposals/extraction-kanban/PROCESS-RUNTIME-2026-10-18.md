# CUT-02-R6-C：真实Node/Bun环境与输入回执

输入主仓`bd36bc171`（A实现为`f0bfa9bbb`）、库`10bbb32 / 0.6.4`。库最终提交`ca238c0 / 0.6.6`已正常推送既有交付分支；主仓只消费固定vendor包，没有兄弟源码引用。R12-U1继续暂缓。

## 问题归属与修正

| 记录 | 归属 | 原因、修正与边界 |
|---|---|---|
| PC-01 | 原有Bun物理适配缺口 | Bun1.3.14异步child_process丢失合法`__proto__`，即使env为null原型也一样；原生Bun.spawn保留。Node继续child_process，Bun改用真实原生接口。环境在库层仍为完整快照，宿主哨兵不继承，argv不经过隐式shell。 |
| PC-02 | 原有主仓合并缺口 | NodeProcessHost使用`{}`赋值会丢特殊键，显式非法覆盖又会被静默修剪，随后用宿主值执行。改为null原型快照；ambient继续筛有效项，explicit完整交库的唯一校验，在物理启动前拒绝。Surface如实编码已有Environment字段，拒绝访问器和非字符串数据、不执行coercion，输入/视图不泄漏别名。 |
| PC-03 | 旧测试前提错误 | Bun回调可在目标消费前确认管道写入，杀目标不必倒推出Unknown；Bun worker的closeSync(0)也未建立真实断管。现在先证明目标实际仅读一字节且大写入未确认，再杀；断管使用真实POSIX关闭fd0且目标继续存活。Unknown/EPIPE明确断言，不接受两种结果之一、不自动补写。 |
| PC-04 | 原有验证盲点 | Bun的Node ESM monkey-patch不能证明spawn为零；观察两种真实原生入口并增加一次有效启动正控，再检验非法输入零spawn。环境以真实子进程回报证明，不靠未命中的拦截对象。 |
| PC-05 | 本轮实现接线遗漏，已修 | 0.6.5基础Process入口及安装消费者通过，但主仓真实监管目标第二跳仍走NodeCP，特殊键继续丢失。该候选未交付，固定原包留存。0.6.6共享唯一私有原生I/O文件，基础会话detached:true、监管目标detached:false；Worker保留自己的事实与转发，不复制ProcessSession。同步spawn异常仍是failed/spawn/ENOENT，不降格Unknown。 |
| PC-06 | 原有Bun Capture P1，新反例确认 | 首次takeOutput前真实写者已同步写完64MiB并落marker，observed仍0；旧0.6.4与本候选都复现，Node会真实阻塞。Bun原生pipe和lazy三种对照也失败。已在规范原条款和文档明确尚未兑现，单列CUT-02-R7，不以块大小或70/71项通过冒称背压/总内存界限。 |

I/O文件有每个child的事件状态，不称无状态；只共享这项实现，不新增公开万能spawn入口。构建只允许`../native-process/BunProcess.fs`这一确切路径，不开放任意兄弟源码。去掉ProcessSession/OutputTail/Process Native源码后，监管工程仍独立编译并通过实际消费者，非白名单路径明确拒绝。

## 实际证据

日志位于`/private/tmp`，重复运行不加总，TODO/skip不计通过。

| 范围 | 结果与日志 |
|---|---|
| 原生原因与原型 | `process-c-20261018-env-native-{node,bun}.log`、`env-bun-spawn.log`；`bun-adapter-prototype.log`的二进制回显/特殊键/部分写入/真实断管4项通过。未await原生FileSink的初始诊断错误另保留，不作产品结论。 |
| 主仓原反例 | `process-c-20261018-main-env-red.log`：真实Node目标丢__proto__；主仓合并修后Node3/3。`main-env-bun-oldlib.log`仍12pass/1fail，0.6.5的真实监管目标亦失败，`main-bun-final.log`保留。 |
| 最终库物理口 | Node/Bun各71/71：`process-c-20261018-final071-{node,bun}.log`；覆盖输入、EOF、别名、上限、重叠、断管、输出、exit/close及同组TERM/KILL。 |
| 最终库全套 | 337项336pass/0fail/1Linux限定skip/0TODO：`process-c-20261018-library-final-tests.log`。七入口Fable构建通过；本机不能运行的Linux限定测试不记通过。 |
| 独立编译边界 | 3/3：`process-c-20261018-independent-build-final.log`，Process与监管去兄弟owner源码、唯一共享文件、越界路径负控。 |
| 精确库包 | 0.6.6、22files/103,320bytes，SHA-256 `4a6d7f8cc8bd9705606fa76abd6740449b747d1583568e148754b4a268722abc`。0.6.5/2e9bef仅为失败候选，未覆盖或放入最终vendor。 |
| 非作者实际库包消费 | `review-process-c-supervision-install.log`：Node/Bun各4组，自写公开消费者、无仓源码；最终target特殊env/argv、无消费者ENOENT清理、双路共1,048,586bytes、目标先退出后同组后代TERM→KILL→ESRCH。22个安装文件及archive摘要不变、临时目录清理。基础入口的独立0.6.5四组证据另留，不替代该监管消费者证明。 |
| 主仓最终原生回归 | `process-c-20261018-main-066-related.log`：128文件997项900pass/0普通fail/25skip/72TODO、exit0。GAP-092的549/256仍是失败TODO，不算修复。 |
| 最终Bun真实主仓环境 | 完整011为14/14：`process-c-20261018-main-066-bun-env.log`。 |
| 精确主包与实际消费 | `process-c-20261018-main-package.log`：generation622，2076files/13,384,236bytes，SHA-256 `c9d93ad3e9daaa5784eaff3e9c0df1d837fa2bf1197b20b2d6b4615902b37c14`。`main-isolated.log`：真实隔离安装，Node/Bun完整011/014各26项全过；完整PTY准入/重启124/124；只加载包内0.6.6，2076安装文件和archive不变，目录清理。 |
| 真实OpenCode Host | `process-c-20261018-real-host-final.log`：1/1，网络provider请求、原件与新OS冷读；首次使用错误tier变量产生的skip不计通过。 |

第一次主仓扩大运行在完整构建结束前启动，模块尚未物化，失败日志`main-related.log`/`main-bun.log`保留为本轮执行次序错误；构建622完成及freshness通过后才取得最终证据。源码、正式入口、原生诊断、隔离安装各自记账，不合并成全系统通过。

## 复审与未完成边界

`review_process_a`独立发现PC-02显式非法覆盖静默丢弃，修后真实Node三项3/3，并验证编码反例与别名隔离；`review_bun_adapter_c`独立发现PC-06并以旧包及Node作对照，完成最终共享边界及精确0.6.6监管消费复审，未确认新增回退。额外80次连续写入/EOF、80次快速退出及160闭合实例描述符观察通过，不冒称完整RSS测量。

扩大Bun九文件组合仍未全绿：`process-c-20261018-main-066-bun-nine.log`为106pass/6fail/4导入错误。非作者精确旧/新包完整009中Node各17/17；Bun两包都未命中两项fs named-binding故障拦截。新包另一次128MiB计数不符，随后旧包也复现实际writeSync回执134,217,728而spool仅91,488,256；不能只归因短写，也不能认定本轮引入。日志`process-c-independent-009-{before,after}-bun.log`及`process-c-independent-output-receipt.log`保留，继续作无库原生对照，归入R7的大输出真实性边界，不把失败变成绿。多文件node:test上下文错误仍未取得正式Bun证明。

正式Node定向四文件31pass/0fail/1integration skip、4/4完成、owned收尾accepted=true，日志`process-c-20261018-formal-scoped.log`；最终format/check通过。Bun正式监督仍归CUT-00-R2，Node正式包装器有界等待归CUT-00-R1；本轮未放宽静默窗、伪造verdict或消除原Unknown。

环境/argv/stdin及声明的真实监管接线已验证，扩大Bun大输出仍需分层归因，C维持REVIEW，不关闭Process父卡、GAP-092、Bun背压、主动脱组、完整Bootstrap或其他平台。库与主仓正常提交推送；这是有限范围修复，未宣布项目通过，未部署用户Host。
