# provider-attempt-recovery 测试

WHAT 是规格；本目录只说明现有用例实际覆盖什么，不新增规则。文件按 WHAT 编号对应；[012] 已并入 [009]，[024]/[025] 无条款（程序注释中的 provider-attempt-recovery-024 字样描述的加载期废弃行为由 [023] 的 boot 恢复扫描覆盖，整体定夺规则另见 GAP.md「进程重启与未终结执行的加载期定夺」）。

- 001—007：预算代数、持久折叠、账本准入与磁盘重开。005 断言当前实现的上限是 10（defaultBudget），并把耗尽后的拒绝与重开保持一并验证。
- 005/019：经薄接缝调用生产 retryAttempt，注入可暂停的 admission 与 redispatch 端口，观察授权前不重投、非 provider 失败不进入 admission、同一份精确许可被传递。这里的 redispatch 是受控端口，不是实际宿主发送，也没有接真实账本。
- 006/013：实际 planner 的新建身份观察及不同 request kind/role 的对照，不覆盖跨物理重试保持身份。016 直接读取生产 RequestKind 的成功计账判别。
- 008：实际内容有效性分类（classifyOutcome、terminalValidity）。工具错误不等于 provider 失败。真实 Manager repair 与 reconciliation 的全链装配仍缺。
- 011：计划冻结/绑定/消费；不覆盖完整发送。
- 019 的 retry-owner 扫描器（`scripts/checks/retry-owner.mjs`）按当前路径与词形做启发式检查，两项违约 fixture 只证明已识别模式会报警，不覆盖所有调用方。
- 020：同一事实序列折叠及计划操作前后的 manual 计数；零 manual 不排除后台 resume，重启副作用边界仍缺。
- 021：真实路由 runtime（acquireExecutionAdmission 等）、durable same-provider retry 判别。注意：程序侧 recoveryStep 仍保留「二次失败拉黑换候选」策略，与 WHAT [021] 的原样重试口径不一致（WHAT 内已标【程序待改】）。
- 022：真实进程内 ProviderAttemptStopFence（wanxiangli/failure），覆盖其它 key、重复、迟到、撤销后不能复活。尚未连到真实宿主事件和物理发送。
- 023：真实 SessionRecoveryHostSurface 对指定 session 的窄化扫描、端口调用与 manual 处置。无 capability 时仍为 Accepted 的反例与 managed-chat-execution-012 的 manual/blocked 合同需统一；boot 后自动扫描与进程重启仍缺。

各文件的 test.todo 登记生产链路缺口（真实宿主终态观察、跨重试身份保持、boot sweep 等）。用 `requirements/verification-system/tests/run.mjs` 的 `TESTS_MJS_FILES` 选择本包编号文件，节点前先刷新正式构建；局部直接执行只作诊断。TODO 不计为通过。
