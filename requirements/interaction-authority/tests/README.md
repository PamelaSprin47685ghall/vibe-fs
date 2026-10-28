# interaction-authority 测试说明

WHAT 决定来源、身份与生命周期边界。本目录区分纯转换、真实 journal/dispatcher、Host 受控端口和完整插件执行；它们不能互相代替。

- 001/002：真实 dispatcher 把字面完全相同的 receipt 与 physical ID 作不同处理；receipt 经 journal 重开仍不建立 root，确切 physical acceptance 才建立。文本样例另验证输入分类。
- 003/005/006/015：真实 root、owner 校验、拒绝/未知发送、活跃用户续行；003 验证 root 与完整 identity 经持久化重开后一同恢复。既有 codec 用例验证 current/legacy 格式。
- 004/008/009/010/012/013/016/017：纯来源、claim、continuation 与 exact identity。013 比较完整 evidence，017 先建立真实可查的续行再 close。
- 011 的 chat-params 用例执行 Host 适配器，但不覆盖原子 AttemptExecutionProfile 全链。014 的资源文本检查只证明当前提示包含相应说明。
- 018：纯 exact-close 的正反例，并加 fast-check 不变量：任意 authority kind / manager 位 / agent-owner life / 记录值下，最终记录的 closure kind 必须属于同一 root kind（AgentOwnerRoot 永不落到 HumanRoot 类）。
- 019：CompletedTurn 分类（正文、reasoning、工具活动、终态/在途、当前及历史 role 观测反例）；并发 nudge 用例使用实际所属 session。
- 020：实际 settlement → report → kill 与注入边界（当前为 TODO）。
- 021：实际 EventStore 关闭重开后保留历史 payload，新 ingress 拒绝旧身份；历史 Inspector fixture 保存事实解码失败反例。
- 022：road binding 与 handle 转换的局部验证，不含真实 resume、崩溃重启或并发恢复。
- 023：调用真实 CompletedTurn 的 suppression/defect 判定，分别观察稳定 terminal 与 durable terminal 解除抑制。
- 024：全部角色、无文本／只有 reasoning／真实工具调用及连续空终止不耗尽；`requirements/host-boundary/tests/omp-scope-seam.test.mjs` 的 `interaction-authority-024` 通过真实注册的 OMP `turn_end` hook 连续触发 35 个新空回合，确认每回合一条 agent-attributed nudge、同回合重放不重复、真实工具/正文/人工取消不误触发。

运行：先 `node scripts/build.mjs`，再用 `requirements/verification-system/tests/run.mjs` 指定本目录全部编号测试；集成用例需 `WXS_TIER_INTEGRATION=1`。
