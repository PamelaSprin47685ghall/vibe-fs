# managed-session-lifecycle 测试

规则以本包 WHAT 为准。这里记录证据范围，不补充新的生命周期规则。

## 当前证据

- 001/005 调用真实 AttachedSessionRuntime：同 owner 重用、保持绑定的 agent、显式移除后重新创建。005 还用同一个实际 owner 验证不同 scope 隔离、同键并发仅创建一次，以及不同 typed Role 的绑定可分别重用和移除。后者证明准入后的资源键，不授予 Coder/Inspector 活跃身份；[delegation 007](../../delegation/tests/007.test.mjs) 和 [participant-identity 010](../../participant-identity/tests/010.test.mjs) 另证旧角色活跃准入被拒。`retainBinding=false` 表示主动 Remove，不能证明系统检测到永久丢失。
- 002/003/011 调用真实 satellite owner，端口提供 association 与 Host 观察，验证单次创建、确切复用、拒绝冲突和替换结果。端口数组不是一个共同的顺序日志；没有真实 journal 重启或首 prompt 悬置证明。
- 004/009/014 运行同步委托与部分真实 journal。006—010/015 的 fold、codec、视图和属性测试证明局部状态转换，不是实际父取消、Join 交付或进程恢复。
- 016/017 调用实际 Session adapter/termination，悬置清理与 Host abort，观察终态不得提前发生、完整 Authority Root 和根会话拒绝。016 删除了只改变测试变量的“虚拟时钟”断言。017 尚未接真实父等待与全部 successor 入口。
- 018 实际 fork tool detach 后读取 durable Active，断言未 abort；另经真实 Scope.CancelSessionChildren 发起受权取消，在 Host abort 端口受控悬置时读取目标 child 的 durable Abandoned，另一个 owner 的同名 child 仍 Active，取消必须等端口完成才返回。两个隔离违约分别漏写 abandon、丢弃 cleanup 等待，用于独立验证断言；这不证明 OS 进程退出、全部 PTY 清理或重启恢复。020 使用实际插件重开 journal，加纯 authority 属性测试；它不证明尚未落地的五类 durable exact closure。
- 019 保留实际 execution interpreter 的局部验证，完整 cancel/delete 入口见 GAP-126。021 的真实 fatal 链仍缺证明。022 运行真实 Casebook：有/无 draft、Bookkeeper 不可用、归档读取与重复拒绝；尚未观察 identity 去留及五类 typed settlement。
- 023 调用实际身份解析，区分拒绝旧活跃身份与历史恢复。024 只证明 handle 拒绝第二个活动绑定及 road 保留给定模型；不是 crash、模型固定或 PTY 排空证明。
- 025 保留上游的真实 HostForkRuntime 与 ToolRuntimeScope 场景：受控 PTY port 结束后清记账、DevOps 返回排空自己而保留其他会话。端口未启动 OS 进程，不能证明 TERM 后真实退出或升级 KILL，物理链明确保留 TODO。

006/007 有可执行失败 TODO：相同 HandleLinked 重放能复活 Retired，或重开 completed cell 接受另一 completion。见 GAP-132；不计通过。缺失的整链证据集中在 GAP-133；测试接缝修正在 GAP-134。DevOps 模型与 prior-run closure 另见 GAP-129/123。

## 执行

先使用仓库正式构建，然后通过 verification-system/tests/run.mjs 的 TESTS_MJS_FILES 指定本目录条款测试。需要跨插件的用例启用 WXS_TIER_INTEGRATION=1。TODO、未完成文件及静默监督中止均不构成完整验收；局部直接运行仅用于定位问题。
