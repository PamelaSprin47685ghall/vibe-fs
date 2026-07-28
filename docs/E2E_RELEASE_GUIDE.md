# 0.4.0 E2E 加强与发布操作指南

本指南是发布门禁，不是愿望清单。任一阻断场景没有直接证据即为 **No-Go**；不得以单元测试或旧 `build/` 产物替代真实 Host 证据。

## 0. 前置条件

```bash
npm ci
npm run build
npm run test:compile
npm run test:next
npm run test:manager-tools
node testkit/opencode/tests/gate-testkit.mjs
CANARY_REPEAT=3 node scripts/run-canary-staggered.mjs
```

每次运行须从干净 checkout 开始，记录：commit SHA、Node/.NET/OpenCode 版本、模型 A/B/Blogger 配置、随机 seed、每个 scenario 的临时目录和完整日志。任何失败先收割所有 scenario 的 diagnostics，再判整轮失败。

## 1. Canary 启动链

验收 `scripts/run-canary-staggered.mjs` 日志：

1. 第一个 canary 立即启动。
2. 后一个 canary 只在前一个 stdout/stderr 输出**独立一行** `[setupScenario] ready` 后启动。
3. 前一个 ready 后继续运行；不得等待退出。
4. ready 前退出或 10 秒内未 ready：该 case 失败，但启动门释放以收集后续诊断；本轮必须失败。
5. 三轮均保留启动顺序、ready 时间、退出状态和 stderr。

## 2. Host reconcile

使用严格 Mock OpenCode 和真实插件运行以下轨迹：

|轨迹|断言|
|---|---|
|idle → Unknown → Unknown → terminal|同一次 idle 内第三次 snapshot 产生一个 completion|
|idle → Unknown ×3|无 completion；dirty latch 保留；下个 idle 可再次读取|
|重复 idle ×3|单飞；同一 assistant completion 恰好一次|
|deleted during reconcile|binding、listener 与 pending state 被清理；无 completion|
|abort terminal|产生 abort 事实；不写 fallback failure；不发 zero-width repair|

同时保存原始 `session.messages` fixture，尤其 tool part 的 `state.input`、`state.output`、`state.status`，并断言 canonical/provider-visible projection 中保留模型可见 tool input/result。

## 3. Companion

**Authority gate.** Companion eligibility reads only `ActiveLogicalRun.Profile.Agent`. Missing ActiveLogicalRun means no Blogger and a `MissingAuthorityProfile` diagnostic. Do not infer role from `sessionRoles`, last physical user agent, transform input agent, or child linkage.

每项必须比较实际送到 provider 的 provider-visible bytes，而非 Host timestamp/runtime metadata：

1. 初始 delta 产生 Blogger B；B 只含 Blogger assistant 正文。
2. Blogger busy 时连续写入三 turn：只启动一个 Blogger；空闲后的下一次 delta 覆盖所有跳过内容。
3. 第一次 epoch：coverage 只截断完整 semantic turn；原始 raw tail 保留。
4. 已有 epoch、Blogger busy/failed、新增三 turn、再触发阈值：新 epoch 只能截断 `LastSuccessfulProjection` 已证明覆盖的最长前缀；三 turn 均逐字节留在 raw tail。
5. 同 epoch LatestB 增长与 self-rebase 都不改变 `companion-b-head` 的 ID、role、parts、正文或顺序。
6. 旧/外来 `companion-b-head-*` fixture 不得被视为当前 epoch 的幂等命中；必须 fail closed 或走明确清理路径。
7. 重启后恢复同一个 Blogger；若 Blogger 不存在，发送 full reset，不得将 delta 发给空白 Blogger。

## 4. Fallback A/A/B/B（Logical Run attempt，非 Session 永久 Side）

Fallback 属于 **Logical Run**，不属于 Session 永久状态。

冻结规则：

1. `session.status=retry` 写 durable `FallbackFailureRecorded`（唯一写入口）；
2. 同一 Logical Run attempt 映射：1→A，2→A，3→B，4→B，5→禁止；
3. identity = `logicalRunId + AuthorityRootUserMessageId + providerAttempt`；
4. 新 Authority Root 始终新 epoch：`Failures=0, Side=A`；
5. 真人显式 model 永远优先；
6. 真人省略 model 只继承 `LastAuthorityProfile.BaseModel`，**绝不**继承旧 Run 的 Side B EffectiveModel；
7. Continuation / B retry 不写回 LastAuthorityProfile。

可接受 canary 轨迹（`fallback-canary`）：

```text
Authority Root A → fail → same-run A retry → fail → same-run B → fail → same-run B → Dead
new Authority Root（omit model）→ inherits BaseModel，epoch resets to Side=A
new Authority Root（explicit model C）→ BaseModel=C，epoch resets
```

**仍是发行阻断**：必须证明同一 Logical Run 内真实 provider request 为严格 `A A B B`，且没有第五个 request。不能用「下一真人 prompt 才切 B」冒充同 Run A/A/B/B。

## 5. Review witness

真实 Reviewer scenario 必须保留 journal facts 与 Host prompt IDs：

1. 同一 assistant message 内两次 PERFECT：不确认。
2. 两个 provider run，但第二个 root user message 不是 guard confirmation prompt：不确认。
3. 第一 PERFECT → guard prompt accepted → 新 provider run，其 root user message 等于 confirmation prompt ID → 第二 PERFECT 确认。
4. 缺 ProviderRunId：tool 返回错误，不能降级为 ToolCallId。
5. tree 变化、REVISE、rebase（即使 tree hash 相同）均建立新 barrier，要求新的两次 PERFECT。

发布证据应包含 ManagerJob、Reviewer session、barrier、tree、两个 provider run、两个 tool call、confirmation prompt message ID 与第二 root user message ID。

## 6. Inspector 与 Executor

在隔离 Manager worktree 内运行 Coder → Inspector：

- Inspector session `Directory` 与 Executor `pwd` 必须等于 Coder worktree。
- Inspector schema 精确为 `{executor}`，没有 fork/join/list/PTY。
- 并发两个 Inspector；结果顺序保持与 prompts 顺序一致；不污染 Manager join mailbox。
- 取消 Coder 后等待 Inspector abort，确认 child session/listener 不遗留。
- 捕获 Parent B snapshot digest；create retry 必须复用同一 snapshot。
- 对相同 `processId|level|start|end`，Executor ID 必须稳定为 SHA-256 派生值。
- map/reduce 乱序完成时，以 chunk index 还原顺序；map/reduce 失败时返回 partial summary、已完成摘要和最后 200KB raw tail，不丢 ProcessResult。
- 输入拒绝 NaN、Infinity、负数；巨大有限 estimate 仅受 cancellation 限制，不能在 int/TimeSpan 转换处溢出。

## 7. PTY

仅 DevOps schema 可见 `fork-pty`。Manager 只 `fork(devops)` 委派。逐个验证 `TERM,KILL,INT,HUP,QUIT,USR1,USR2`：

- TERM 默认；5 秒后无 exit 才 KILL。
- Signal/Close 不发布 completion；只有 backend `onExit` 发布。
- Close 等价 stdin EOF。
- 两次 read 返回 unread delta；UTF-8 半字符跨 read 不损坏；buffer 大于 64KB。
- parent abort 后 await process-tree 收敛，无 PID/port/PTY handle。

## 8. Orchestrator

用真实 Git 临时仓库覆盖：

1. dirty target 拒绝下一 user message；不 stash。
2. 两 Manager 并行 worktree，publish lock 使 target ref 串行。
3. 每一 review skip 同时匹配当前 tree 与 barrier；修改 tree 后必重审。
4. conflict file 查询失败必须 fail closed，不得当作空冲突。
5. rebase 后新 barrier + 两次新的 PERFECT，再 `--ff-only`。
6. 注入崩溃：candidate、pre-review、rebase、conflict、post-review、ff、Published fact、worktree cleanup、branch cleanup 的前后各一次。
7. 重启后由 Git authority 识别已发布；不得重复 ff；已发布但 cleanup 失败必须报告 cleanup pending 并完成清理。

每场景 dispose 后检查：child PID、port、SSE、pending request/session、PTY、spool、worktree、manager branch、rebase state、publish lock 全为空。

## 9. 打包与版本

仅在第 1–8 节全部通过后：

```bash
npm pack --dry-run
npm pack ./build
mkdir -p /tmp/wanxiangshu-install-test
cd /tmp/wanxiangshu-install-test
npm init -y
npm install /absolute/path/to/wanxiangshu-<version>.tgz
node -e "import('wanxiangshu')"
```

发布 manifest 必须 `private: false`；公开发布必须有选择后的许可证文件与一致的 `license` 字段。最终版本升为 `0.4.0` 前，在新的干净 checkout 重跑整套门禁，不能复用 RC build。
