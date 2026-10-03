# durable-events 测试

WHAT 是验收依据；本目录说明当前证据，不额外规定实现。

001—018 主要驱动实际 canonical codec、EventStore、Journal、payload、Git Hook 和 Integrator：包括字节身份、损坏拒绝、提交门禁、提交前后 Current、并发分支、retention、合并顺序及物理操作计数。临时目录与受控故障用于隔离被测执行；正常 dispose/reopen 不是进程崩溃证明。

019 让 Structural、Strength、Casebook、JsTransaction 的实际事实改变 Current，并重开同一磁盘历史核对；Journal 也实际追加后重开。反例经 WorkspaceEventStore 的 programWithoutRegistration 观察口在各自临时目录逐一移除真实 hostProgram 注册：移除 Strength/Casebook/JsTransaction 时 live fact 仍按已知词汇落盘，但对应业务 Current 缺失（decision 为 null、case 为 null、pending 为空），保留注册的 Structural head 不受牵连；移除 Structural/Journal 时构造被 CanonicalIntegrator 的 base-rule 前置检查整体拒绝，测试如实断言该 fail-closed 形态而非伪称缺失 Current。同文件静态 gate 有合规/违规样例，只覆盖所识别的形式。

020 检查 Journal boot 不创建 writer，首次业务 append 才落盘；实际 workspace capability 在获取时容忍尚未消费的损坏历史，在消费时拒绝。已识别的历史 Journal fact 经过激活重放而非仅 boot。没有通过这些局部入口宣称整个插件加载流程已验证。已删除以 lazy、特定 decoder 名称或函数片段充当行为与性能证明的用例；这些可替换实现不是额外验收要求。

021 证明坏事实与 cut 一起落盘、同进程重开可重放重置，另保留历史错误作用域用例。产生坏事实的实际进程退出、下一代进程恢复仍为 TODO；低层 store 继续 append 不代表上层进程获准继续运行。

022 读取真实工程声明的传递闭包与源码数，并实际隔离编译 Journal 观察 owner；未实际编译每个 locality。此真实编译使用已有 compiler 预算单独监督，不放进普通 5 秒静默组。023 的 codec 用例实际覆盖公开协议，日志 port 用例实际覆盖查询与提交；部分源码扫描只证明当前词形。023已有同一物理store probe在store闭包编译成功、在codec闭包被编译器具名拒绝的正式用例，domain fold缺少aggregate authority的编译证明仍TODO。

2026-10-03合并增量：日志port的原子发布fixture先经真实dispatcher接纳父子Root，随后仅暂停目标HandleLinked的追加；三个canonical handle视图同时变化，revision精确增加一次。023的真实Fable编译误入default unit，使七文件诊断在只剩该编译lane时触发5006ms因果静默，原始失败不抹去；该case现归integrationTest，完整integration自动发现并执行，原positive/negative、具名诊断与源码未改断言全部保留，不扩大预算。结果与边界见[同步记录](../../../proposals/archive/2026-10-03/Upstream同步-e1e7dd3f1-2026-10-03.md)。

024—025 不再用自建 fatal 模型或源码调用字样证明结算与退出。025 实测的是同一 Prepared 身份不同载荷导致 StorageInvalid，并检查事件文件完全未变；它没有产生 semantic cut。真实 cut 的 typed 传播、必需注入、事务副作用边界和一次终止仍待证。

完成一个节点后先运行 `node scripts/build.mjs`，再通过 `requirements/verification-system/tests/run.mjs` 将本目录 NNN.test.mjs 交给 `TESTS_MJS_FILES`运行默认层；真实编译走 `requirements/verification-system/tests/integration/run.mjs`的既有compiler预算，不在unit组强开tier。TODO不计为通过；发布canonical-spine节点保持TODO。与跨进程、结算和架构相关的缺口见GAP-097/098。
