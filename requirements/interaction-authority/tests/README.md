# 交互权威的用例与证明范围

WHAT 决定来源、身份与生命周期边界。本目录区分纯转换、真实 journal/dispatcher、Host 受控端口和完整插件执行；它们不能互相代替。

- 001/002：真实 dispatcher 把字面完全相同的 receipt 与 physical ID 作不同处理；receipt 经 journal 重开仍不建立 root，确切 physical acceptance 才建立。形态测试不能替代这一边界。文本样例另验证输入分类，不宣称已覆盖所有真实 Host 来源。
- 003/005/006/015：真实 root、owner witness、拒绝/未知发送、活跃用户续行；003 新增 root 与完整 identity 经持久化重开后一同恢复。既有 codec 用例验证 current/legacy 格式，不能替代原子 append 的所有 crash cut。
- 004/008/009/010/012/013/016/017：纯来源、claim、continuation 与 exact identity。013 比较完整 evidence，017 先建立真实可查的续行再 close；旧的“从未登记 key 在空状态里查不到”不再充当关闭证明。
- 011 的 chat-params 用例确实执行 Host 适配器，但不是原子 AttemptExecutionProfile 全链证据。014 的资源文本检查仅证明当前提示包含相应说明，不证明 Agent 必会等待后台任务。
- 018 保留纯 exact-close 的正反例。它没有写入 WHAT 的五类 source witness 或 AuthorityLogicalRunClosed；这些不是已完成能力（GAP-123）。
- 原 004 的 CompletedTurn 分类测试移至 019：保留正式文本、reasoning、工具活动、终态/在途、当前及历史 role 观测反例，不把旧 role 观察转换当成允许新准入。并发 nudge 测试使用实际所属 session。XML-only 与正文工具标签的现行区分沿用 25-D1 待议，未用测试改合同。
- 020 删除只验证自造 descriptor 的伪证明，保留实际 settlement → report → kill 与注入边界 TODO。
- 021 实际 EventStore 关闭重开后保留历史 payload，实际新 ingress 拒绝旧身份。另以正式历史 Inspector fixture 保存事实解码失败反例（GAP-124）；存得下不是完整读得回。
- 022 的 road binding 和 handle 转换是局部证明，不能声称真实 resume、崩溃重启或并发恢复已完成。
- 023 调用真实 CompletedTurn 的 suppression/defect 判定，分别观察稳定 terminal 与 durable terminal 解除抑制；不等于已证明真实 retry、nudge 派发及 occasion 去重整链。

GAP-122 汇总缺失证据；GAP-125 保存无 active target 仍可发送 continuation 的实际反例。TODO 均不计为通过。

正式验证：先构建，再用 verification-system/tests/run.mjs 的 TESTS_MJS_FILES 指定本目录全部编号测试，联查 requirement-system、participant-identity[010]、dispatch-protocol[003] 及 31 的 hook/stop 边界。不把辅助资源检查、文件存在或纯状态数组当作实际运行证明。
