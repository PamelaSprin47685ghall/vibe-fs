# Managed Chat Execution 运维操作说明

本文档供运维与开发人员在日常运行、排查异常及确认执行完成时参考。

## 怎么起（编译与运行）

1. **构建与检查**：
   运行仓库规定的构建与代码检查脚本：
   ```bash
   npm run build
   npm run check
   ```

2. **宿主插件就绪与激活**：
   - 宿主（如 OpenCode/万象通）加载插件时，由 `HostSignalBootstrap` 触发底座激活事件 `DurabilityActivated`。
   - 恢复系统在持久层底座激活后自动折叠未终结执行，完全由投影、容量变动及外部回执等事件因果驱动，无需人工手动触发恢复，严禁使用定时器或轮询。
   - 单条物理消息的正常受理完全由 `ChatAdmissionTransaction` 驱动，按严格次序（身份解析 → `Accepted` 落盘 → 获取容量 → 宿主投影 → 提交租约 → 触发 provider 动作）流转。

3. **进程重启与重载**：
   - 遇到异常时，通过正常的系统/进程管理命令重启即可。
   - 重启后旧进程持有的内存租约、等待器、取消令牌与回调全部自动作废；新进程只从已落盘的持久化事实重建状态。严禁手动注入虚假事件或调用未受控私有接口。

## 看什么（状态观测与现场采集）

1. **查看单个消息执行状态**：
   - 执行身份仅由 exact `(sessionId, physicalUserMessageId)` 标识，不能按会话模糊归并。
   - 通过 `StatusSurface.queryFacts(serializedFacts, sessionId, physicalUserMessageId)` 观察当前状态：
     `{ accepted, providerStarted, terminal, disposition }`。

2. **查看未终结执行**：
   - 通过 `Surface.nonTerminal(serializedFacts, sessionId)` 筛选处于 `Accepted` 或 `ProviderStarted` 的未终结执行列表，确认是否存在未完成任务。

3. **查看容量与租约健康度**：
   - 通过 `ModelRoutingSurface.sharedCapacitySnapshot()` 观测当前活跃租约、排队等待器与所有者分布。
   - 通过 `ModelRoutingSurface.reconcileCapacityEvidence(snapshot)` 检查容量一致性；正常情况下返回 `NoOp`，若返回 `FailClosed` 则表明租约超出范围或计数异常，需立即排查。

4. **查看人工介入（ManualIntervention）记录**：
   - 检查 `PluginRecoveryScope.ManualChatInterventions()` 中是否存在未处置记录。当外部回执缺失/模糊、持久化或物理状态未知、缺乏策略授权时，系统会生成 manual intervention 记录并安全封闭。
   - 当对应执行成功落盘终态后，该 manual 记录应自动被撤销。

5. **事故现场采集（官方 npm 脚本）**：
   - 当出现状态不一致、执行挂起或崩溃时，使用仓库官方采集脚本收集全息现场（自动脱敏 API Key、Token、Prompt 和调用栈）：
     ```bash
     npm run diagnostics:collect -- --run <runId>
     ```
   - 检查生成的报告包，重点比对持久化事实投影与容量快照。

## 常见异常处置

1. **仅有 `Accepted` 但未启动 provider**：
   - 确认宿主侧是否确实未启动 provider。
   - 若宿主具备恢复能力（`ExactAcceptedMessageRecoveryPort` 接受），系统自动重入准入流程；若宿主不支持，系统记录 manual 标记并安全封闭。严禁手动重发消息或向账本补写虚假状态。

2. **执行已达终态但容量未释放**：
   - 确认该执行是否已持久化落盘 terminal。
   - 确认容量对齐为 `NoOp` 后，通过容量所有者按 exact key 触发精确释放，严禁按会话盲目整池释放。

3. **物理标识冲突**：
   - 同一 exact key 出现冲突事实时，系统保持首个落盘事实并安全封闭（fail closed）。严禁手动覆盖既有事实或重置冲突计数。

## 判定做成

系统正常完成执行的判定标准：

1. **单物理消息完整闭环**：
   物理消息经 `Accepted` 落盘 → 取得模型容量 → `ProviderStarted` 落盘 → 收到真实执行结果 → 写入 `Terminal`（Completed/Cancelled/Failed/Rejected）→ 精确归还容量。
2. **无遗留悬挂状态**：
   持久化投影中不存在遗留的 `Accepted without Terminal` 或 `ProviderStarted without Terminal`。
3. **诊断表清空**：
   `ManualIntervention` 诊断表清空，无遗留的阻断记录。
4. **进程重启后自洽**：
   旧进程的内存租约全部清空，新进程仅由持久化事实重建，无未授权的工具重放与重复调用。
