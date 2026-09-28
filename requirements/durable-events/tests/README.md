# durable-events 测试

WHAT 是验收依据；本目录说明当前证据，不额外规定实现。

001—018 主要驱动实际 canonical codec、EventStore、Journal、payload、Git Hook 和 Integrator：包括字节身份、损坏拒绝、提交门禁、提交前后 Current、并发分支、retention、合并顺序及物理操作计数。临时目录与受控故障用于隔离被测执行；正常 dispose/reopen 不是进程崩溃证明。

019 让 Structural、Strength、Casebook、JsTransaction 的实际事实改变 Current，并重开同一磁盘历史核对；Journal 也实际追加后重开。尚未逐个移除注册做反例，因此不能声称完成该条的注册必要性证明。同文件静态 gate 有合规/违规样例，只覆盖所识别的形式。

020 检查 Journal boot 不创建 writer，首次业务 append 才落盘；实际 workspace capability 在获取时容忍尚未消费的损坏历史，在消费同一 store 的 Journal Current 时拒绝。合法落盘事实经真实 Integrator 读出 session，原文件字节不变；空仓读取返回已注册的空 Current（available=true、sessions=[]），与缺少 Current（available=false、sessions=null）有别，读取本身已经激活。已识别的历史 Journal fact 经过激活重放而非仅 boot。没有通过这些局部入口宣称整个插件加载流程已验证。已删除以 lazy、特定 decoder 名称或函数片段充当行为与性能证明的用例；这些可替换实现不是额外验收要求。

021 证明坏事实与 cut 一起落盘、同进程重开可重放重置，另保留历史错误作用域用例。产生坏事实的实际进程退出、下一代进程恢复仍为 TODO；低层 store 继续 append 不代表上层进程获准继续运行。

022 读取真实工程声明的传递闭包与源码数，并实际隔离编译 Journal 观察 owner；未实际编译每个 locality。此真实编译使用已有 compiler 预算单独监督，不放进普通 5 秒静默组。023 的 codec 用例实际覆盖公开协议，日志 port 用例实际覆盖查询与提交；部分源码扫描只证明当前词形，没有证明编译器拒绝越界。真实负向编译为 TODO。

024—025 不再用自建 fatal 模型或源码调用字样证明结算与退出。025 实测的是同一 Prepared 身份不同载荷导致 StorageInvalid，并检查事件文件完全未变；它没有产生 semantic cut。真实 cut 的 typed 传播、必需注入、事务副作用边界和一次终止仍待证。

完成一个节点后先运行 `node scripts/build.mjs`，再通过 `requirements/verification-system/tests/run.mjs`，将本目录 NNN.test.mjs 交给 `TESTS_MJS_FILES` 并启用 `WXS_TIER_INTEGRATION=1`。TODO 不计为通过；发布 canonical-spine 节点保持 TODO。与跨进程、结算和架构相关的缺口见 GAP-097/098。
