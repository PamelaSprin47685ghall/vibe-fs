# 已移除入口的索引记录

2026-09-28，对照 upstream `1450f49d` 与共同祖先 `4d2b23f` 核对。下列旧 fatal 入口在两版源码中都已不存在；它们不是本次规范施工删除的生产功能。故从 `fatal-inventory.json` 的入口索引移出，保留历史编号，不复用。

| 旧编号 | 当前源码事实 | 仍未证明的边界 |
|---|---|---|
| F05 | `Interaction/Dispatch/DispatchSurface.fs` 的端口适配不含 `ReportFatalDiagnostic`。 | 实际调度的完整 fatal/结算边界，GAP-136。 |
| F06 | `Interaction/Dispatch/DispatchSessionPort.fs` 只转接发送和终态订阅，接口无 `ReportFatalDiagnostic`。 | 同上；接口删除不证明所有调用者行为。 |
| F08 | `Interaction/Dispatch/OpenCode/SessionNudge.fs` 的 `toDispatchPort` 不含 fatal reporter。 | 实际 nudge 到调度的整链证据，GAP-136。 |
| F33 | `OpenCode/Host/OpenCodePort.fs` 无旧 `observeSdkPromptDispatch` 或 `prompt-async-dispatch-listener-failed` 入口。 | SDK 异步失败到精确调度身份的交付与结算，GAP-136。 |
| F34 | 同文件无旧 `verdictForDetachedError` 或上述 fatal 入口。 | HTTP 拒绝、响应丢失及接受后失败的实际分类，GAP-136。 |
| C01 | `Strength/Persistence/Durability.fs` 的 `publishPrepared` 返回 `Rejected`，`append` 返回 `SemanticRejected`；没有可选 fatal handler。现存 composition fatal `strength-semantic-cut` 仍由 F17 索引。 | 真实 cut 的 owner 传播、结算与一次终止，GAP-183、durable-events[025]。 |
| C02 | `Repository/Knowledge/Casebook/Store.fs` 的 `appendEvent` 在 cut 时返回 `Error`，没有可选 fatal handler。 | 真实 Casebook cut 的 owner 传播与终止，GAP-160。 |

这次核对只说明旧入口不再存在，不表示替代路径已经通过回归，也不关闭以上 GAP。门禁仍扫描全部当前生产源码；重新引入的 fatal 调用必须有当前索引记录。未增加扫描豁免或修改门禁。
