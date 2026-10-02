# speculative-investigation 测试与证明范围

这些测试调用生产 Policy、Frame、Projection、Durability、Replica coordinator、rollout scope 和真实插件入口。测试通过不等于投机全链可发布；特别是手工输入 transcript、turn 或游标，不代表真实 provider 已发送、消费或被 XTrace 捕获。

| 条款 | 当前实际证据 | 尚缺证据 |
| --- | --- | --- |
| [001] | 不健康前提、Shadow、嵌套请求的 K0 裁决和默认设置 | 同一次真实 Work 在 Off/K0/熔断与无优化时的行为等价 |
| [002] | 每个机会前提单独翻转的策略矩阵；未知标签拒绝 | 真正 Work 入口从冻结身份与 target 取得证据；角色白名单有合同冲突 |
| [003] | Collector 的并发配对、跨 request 边界；真实 transform/coordinator 的预算关闭；筛空首批和中间批次后的材料连续编号；真实 Host 中两轮只读工具及错误工具后恢复的预算隔离 | 更多预算与故障截断组合的真实网络证明 |
| [004] | 现行角色的精确 readonly capability、退役角色拒绝、owner attachment、single-flight；Host 原始 system 数组中的双语约束；真实 provider 仅收到 js-predictor | 所有角色、语言及拒绝效果的真实 Host 矩阵 |
| [005] | Frame 规范化、序号、digest/调用标识；完整 canonical 文本的独立 UTF-8 字节边界；真实 Host provider wire 收到五条重定位后的完整交换，参数与结果同源、使用 owner 工具名 | 所有工具结果形态与普通压缩组合 |
| [006] | 真 EventStore/payload closure、冲突拒绝、关闭重开读取；Prepared 不 replay | 模糊写入时实际 Owner 外发被阻止 |
| [007] | 消费证据分类、exact target 裁决、失败轮次 Abandoned、Promoted 持久化 | 模糊 Promotion 时实际 continuation 被阻止 |
| [008] | 真实 EventStore 关闭重开后的 Promoted replay plan；定位、trace-range 和覆盖判断；真实中间请求快照确认消费后，两个决策进入 Promoted → XTrace → Traced，进程重启继续回放 | 普通压缩覆盖后的完整因果闭环 |
| [009] | ToolCallId 重定位、孤儿/媒体拒绝、语义不变；mirror 替换及局部 batches | 真实新决策不会复用旧 Replica 上下文 |
| [010] | 成本公式、预算门槛、控制分组、真实 scope 的 exact-run 计数、会话隔离与重放幂等 | rollout 样本充分性和生产训练流不接纳干预请求 |
| [011] | scope 熔断 first-wins；真实 coordinator 的取消、删除、释放与不可复活终态 | 无因果终态时真实 Treatment 仍等待；Host Canary 真实兼容性 |
| [012] | Candidate/Promoted 只插入准确工具交换，保留普通正文中的“replica”等合法内容 | Owner/Replica 真实输入与完整 Host/EventStore 审计字段 |
| [013] | 真实 Host 三次 Manager 委托（含重启）与一次 Engineer 子 owner 委托：四个 Bound/Prepared、九次 predictor 请求；目标主模型收到五条精确配对的工具交换；错误工具不触发 bundle 熔断，主请求无 500；provider system 有只读约束且工具集合精确为 js-predictor | 更长生命周期、所有角色与普通压缩组合 |
| [014] | 合法 ablation 配置下真实 Strength settings 对 Off/Shadow/DryRun/Treatment 的优先级矩阵 | 不把无效 DAG 的 fallback 当作正常 ablated 场景 |

`Strength.Surface.replicaRuntimeCreate` 使用生产 coordinator，但 CreateChildSession/SendPrompt 是未使用的占位端口；测试使用 `AttachLiveDecision`。它可以证明 attach/turn/transform/delete/dispose，不能证明 StartDecision 启动。`transformApply` 的 abort 端口只记录调用，不证明物理 provider 网络已停止。`013.test.mjs` 的 `resident_manager_predictor_keeps_each_budget_and_readonly_boundary` 则启动真实 OpenCode，使用隔离 HTTP provider 观察外发字节和子会话 transcript；该 canary 不使用真实外部模型。现有纯策略测试中的历史角色只是受控输入，不代表允许新建旧身份。

`008` 集成用例确实关闭并重开同一个 EventStore；消费 turn 和 Traced 游标仍由测试提供，不称为真实 provider 或进程 crash 恢复。`006` 单独保留 Prepared 的关闭重开证明。完整字节测试观察生产送入 digest 的 canonical 文本，用 Node UTF-8 计量独立核对；不另造 Frame 算法。

已确认的失败单独保存为可执行 TODO：

- GAP-159：真实 authority 接受 predictor 为 Engineer。其 root Fission 调用又被 origin 门禁拒绝；目前没有“Fission 已成功越权”的证据。
- GAP-184 / 48-D1：当前策略允许 Engineer，但本包旧 [002] 白名单没有 Engineer，且旧角色不再允许活跃准入。待统一合同，未擅改授权范围。
- GAP-183：其余全链证明缺口。源码词形、重复读取与手写常量不再用作行为已实现的证据。

在统一构建完成后可局部运行：

```sh
WXS_TIER_INTEGRATION=1 node --test requirements/speculative-investigation/tests/*.test.mjs
```

这只是局部验证。正式交付须通过 verification-system 的 runner，选择本包全部编号测试及受影响的 `capability-enforcement/005`、身份相关测试；TODO 不计为完成。Host Canary 指纹相符只证明配置匹配，不证明安装版 Host 已满足协议。

## 2026-09-28 上游迁移说明

基于 upstream `1450f49d` 重新核对 [013]/[014]。该版本的 WHAT [013]、StrengthSettings、Replica runtime 和公开签名仍保留 DryRun；`runtimeBinding` 仍接收九个参数。上游新加的 [013] 测试却断言 DryRun 已删除，并调用七参数 binding 和尚不存在的 DelegationRequested 协议，二者不一致。本次保留实际 DryRun coordinator 的 exact owner/target 正反例和启动全链 TODO，不导入这些错误签名或源码词形断言；也没有恢复旧 Sphinx runtime。

[014] 使用新上游 `resetRegistry` 接口，保留真实合法 ablation DAG 与 Off/Shadow/DryRun/Treatment 的优先级矩阵。上游的新只读委托 schema canary 针对另一套尚未在此版本落地的协议，不能作为当前 DryRun 条款的通过证据。以上是迁移兼容判断，最终测试结果以本工作区统一构建后的正式执行为准。
