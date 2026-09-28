# Recovery 与 Sessions 迁移兼容修复

本轮处理三个已复现的测试失败：crash-reconciliation-011 一项与 managed-session-lifecycle-017 两项。不改 WHAT，不削弱原断言，不处理 Host023 的监督政策。

## 修改与依据

- `RecoverySurface.missingMembers` 原来只自行计算集合差，签名是字符串数组，正式夹具却传入恢复节点。现将节点转为已有领域类型，通过 `authorizeFamilyResume` 构造真实 `FamilyRecoveryPermit`，再调用 `FamilyRecoveryPermit.missingFrom`。签名与现行节点夹具一致；不复制成员准入算法。
- `SessionsSurface.terminationProbe` 恢复为真实 `ManagedSessionTermination.terminate → InjectedSessionPort → Host port` 路径。受控 Promise 分别挂住后代排空和物理 abort，观察各阶段的实际副作用；保留当前上游原有 flattening 和 interrupt probes。失败终态仍携真实构造的 Authority Root，不恢复旧角色或退役协议。
- 实际接缝执行后，Host 拒绝用例暴露 `abortManagedSession` 丢弃错误并一律返回成功。现仅透传现有 `AbortSession` Result；没有 Host transport 时明确返回 Error。终止顺序、后代取消和终态策略均未改变。
- Recovery owner 独立编译发现 `Composition/Turn/Program.fs`、`Observation.fs` 引用未消费的 Persona namespace，按主任务授权删除两个旧 open。Sessions owner 发现 Planner 缺真实 ProbeSelection 依赖，由公共工程负责代理补齐，本轮未编辑公共工程清单。

## 失败与复核

1. 原正式测试对当时 dist 的直接诊断复现全部三个失败：许可返回错误对象；两个终止用例找不到 `terminationProbe`。日志 `/private/tmp/remainder-recovery-termination-before.log`。这是诊断，不是跳过新鲜度后宣称正式验收。
2. 真实接缝迁入、依赖修复后，Recovery owner 的 68 个源文件和 Sessions owner 的 484 个源文件均由仓库合法 Fable 入口编译。Sessions 拒绝 Host 的用例仍失败，实际收到 `ok=true`；日志 `/private/tmp/remainder-termination-before-fix.log`。据此实施适配器的最小修复。
3. 修复后 Sessions owner 再次成功编译，日志 `/private/tmp/remainder-termination-owner-3.log`。Recovery 最终编译日志为 `/private/tmp/remainder-recovery-owner-2.log`。
4. 四份现行正式测试按原字节放入隔离目录，导入真实 owner 临时产物，逐份 SHA256 一致：crash `011/014` 为 6 通过、1 TODO；managed-session `016/017` 为 5 通过、1 TODO。合计 11 通过、2 TODO、0 失败。日志 `/private/tmp/remainder-recovery-after.log` 与 `/private/tmp/remainder-termination-after-fix.log`。
5. 七份修改的 F# 文件 Fantomas 检查及修改范围 diff 检查通过。未修改正式测试、共享 dist、manifest 或全局 compile-order；统一构建及正式 runner 由主任务执行。

## 证明边界

许可用例证明真实领域成员检查拒绝丢失、接受单调增长、拒绝换 handle；不证明完整恢复已核验所有成员，join 每个 effect 边界重新核验的 GAP-149 TODO 保留。

终止用例证明生产终止程序和适配器等待受控后代排空、等待 Host 完成，然后发送带精确 Authority Root 的 Failed；根会话被拒绝且无副作用，Host 成功和拒绝均保留反例。它不证明真实 Host 资源排空、完整 successor workflow 或父监听器已经接通，GAP-133 TODO 保留。
