# managed-session-lifecycle 测试

规则以本包 WHAT 为准。这里记录证据范围，不补充新的生命周期规则。

## 当前证据

- 001/005 调用真实 AttachedSessionRuntime：同 owner 重用、保持绑定的 agent、显式移除后重新创建。005 还用同一个实际 owner 验证不同 scope 隔离、同键并发仅创建一次，以及不同 typed Role 的绑定可分别重用和移除。后者证明准入后的资源键，不授予 Coder/Inspector 活跃身份；[delegation 007](../../delegation/tests/007.test.mjs) 和 [participant-identity 010](../../participant-identity/tests/010.test.mjs) 另证旧角色活跃准入被拒。`retainBinding=false` 表示主动 Remove，不能证明系统检测到永久丢失。
- 002/003/011 调用真实 satellite owner，端口提供 association 与 Host 观察，验证单次创建、确切复用、拒绝冲突和替换结果。端口数组不是一个共同的顺序日志；没有真实 journal 重启或首 prompt 悬置证明。
- 004/009/014 包含同步委托与部分真实 journal。006—010/015 保留 fold、codec、视图和属性测试；GAP-132 另补真实 fork/resume 与 canonical work 消费/重开断言，范围见下文，不等同于实际父取消或 OS 进程恢复。
- 016/017 调用实际 Session adapter/termination，悬置清理与 Host abort，观察终态不得提前发生、完整 Authority Root 和根会话拒绝。016 删除了只改变测试变量的“虚拟时钟”断言。017 尚未接真实父等待与全部 successor 入口。
- 018 实际 fork tool detach 后读取 durable Active，断言未 abort；另经真实 Scope.CancelSessionChildren 发起受权取消，在 Host abort 端口受控悬置时读取目标 child 的 durable Abandoned，另一个 owner 的同名 child 仍 Active，取消必须等端口完成才返回。两个隔离违约分别漏写 abandon、丢弃 cleanup 等待，用于独立验证断言；这不证明 OS 进程退出、全部 PTY 清理或重启恢复。020 使用实际插件重开 journal，加纯 authority 属性测试；它不证明尚未落地的五类 durable exact closure。
- 019 保留实际 execution interpreter 的局部验证，完整 cancel/delete 入口见 GAP-126。021 的真实 fatal 链仍缺证明。022 运行真实 Casebook：有/无 draft、Bookkeeper 不可用、归档读取与重复拒绝；尚未观察 identity 去留及五类 typed settlement。
- 023 调用实际身份解析，区分拒绝旧活跃身份与历史恢复。024 只证明 handle 拒绝第二个活动绑定及 road 保留给定模型；不是 crash、模型固定或 PTY 排空证明。
- 025 保留上游的真实 HostForkRuntime 与 ToolRuntimeScope 场景。DevOps 工作先经真实 dispatcher physical acceptance 建立 scoped work，两处 Surface 显式接收同一 canonical journal，不用临时 Root 或无 journal 的 InstallRun。受控 PTY port 结束后清记账；DevOps run 终态结算时在 PTY 端口边界观察到终止效果，其名下 PTY 计数归零且其他会话的 PTY 存活。端口未启动 OS 进程，**「TERM 后真实退出并按需升级 KILL」这一半仍是未证义务**，现由 `test.todo` 单独保留，不因上面那条已转真而消失。

## GAP-132 的 scoped work 回归

006/007 已移除两处源码中的 todo 选项，保留原完整反例：006 的一处声明通过角色循环实例化为 Engineer、DevOps 两个测试；007 是一个完成 cell 重开反例。源码声明数与运行实例数不是同一统计。原来将裸 link 当成新工作的阳性断言改为重复 link 保留墓碑；真实新工作阳性通过 Manager fork/resume 的实际 accepted prompt 建立，不用 Root 字符串伪造准入。

006/007/008/015 新增的正式断言调用已有 ForkToolSurface 及扩展的 exact work 观察/消费接口：A 完成、B 在 join 前接纳、消费 A、迟到 A、B 完成、两份 exact consume、每个关键点重新打开 canonical journal 比较投影。008 在消费 capability 边界注入确认前拒绝与真实追加后的回执未知，并验证不交付未确认 payload、不二次交付；它不是 OS crash 或半行写盘证明。015 保留无永久丢失证明时拒绝 DevOps 换 child 的反例，并拒绝没有 canonical admission 的 Root。

生产视图不将仅有 HandleLinked 的物理绑定当成活动工作。HandleSurface 的无 scope 视图仅保留历史投影观察，不授予新执行或消费权。旧 completed/retired 的字段、正文引用和墓碑不改写；无 scope 历史不能由附近 Root 升格，新的 completion、consumption、void 与 abandon 都使用 exact work。当前无 scope 结果只读审计，待外部 exact settlement/迁移材料，不能自动消费或发新 prompt。

本次合并已接入 scope vocabulary、Surface 登记与编译引用，并通过统一 Fable 构建。gen72 的 Node22 定向诊断中，013/018/025 与 relay-retirement/009 共 15 passed、0 failed、5 TODO；这不是完整套件验收。009 在同一实际产物上取得重复 abandon 多写事实的正式反例，现由 Controller 按 exact work 保留首终态；旧 A 重试不能影响新 B，异 child 与无准入历史不能发行终态。009/013 共用已有 opaque JournalHandle，snapshot revision 显式导出精确十进制字符串。当前 owner 修复与相邻回归仍待新产物的正式 runner 汇总，原始红绿与构建范围见[本批同步记录](../../../proposals/archive/2026-10-03/Upstream同步-e1e7dd3f1-2026-10-03.md)。008/015 既有 GAP-133 todo 保留，DevOps 模型、完整 prior-run closure、真实进程崩溃与永久丢失替换的物理证据不由这些断言冒充。

## 执行

e1合并的统一gen77正式510文件运行中，本包全部active断言通过，含009旧A重试保新B、013冷重放、018受权held abort、025实际journal/PTY端口隔离；完整36文件integration亦0 fail。具体汇总、TODO及未证OS边界见[同步记录](../../../proposals/archive/2026-10-03/Upstream同步-e1e7dd3f1-2026-10-03.md)。不得以端口隔离证明真实TERM/KILL。

先使用仓库正式构建，然后通过 verification-system/tests/run.mjs 的 TESTS_MJS_FILES 指定本目录条款测试。需要跨插件的用例启用 WXS_TIER_INTEGRATION=1。TODO、未完成文件及静默监督中止均不构成完整验收；局部直接运行仅用于定位问题。
