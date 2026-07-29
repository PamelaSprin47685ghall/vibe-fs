# 0.5.0 E2E 加强与发布操作指南

本指南是发布门禁，不是愿望清单。任一阻断场景没有直接证据即为 **No-Go**；不得以单元测试或旧 `build/` 产物替代真实 Host 证据。

当前开发标记：**`0.5.0-rc.1`**（文档冻结 / RC 开发）。规范 SSOT：`next/Doc/SSOT.md`、`0.5.0.md` §23。

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

每次运行须从干净 checkout 开始，记录：commit SHA、Node/.NET/OpenCode 版本、`opencode.json` Managed Agent 绑定（脱敏）、随机 seed、每个 scenario 的临时目录和完整日志。任何失败先收割所有 scenario 的 diagnostics，再判整轮失败。

**Config Gate（发行阻断）**：启动前 `opencode.json` 必须具备完整 20 个 Managed Agent（公开角色的 `fast-ROLE`/`deep-ROLE` + 内部 `fast-blogger`/`deep-blogger`/`fast-executor`/`deep-executor`），每个 Agent 有非空 model，peer 完整，无 legacy 无前缀名称 / `build` / `plan`。缺一 → fail-closed。

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

**Authority gate.** Companion eligibility reads only `ActiveLogicalRun.Profile` Canonical Role / SelectedAgent. Missing ActiveLogicalRun means no Blogger; this expected fail-closed result stays silent in the user terminal. Do not infer role from `sessionRoles`, last physical user agent, transform input agent, or child linkage.

Blogger 为内部 `fast-blogger`/`deep-blogger`；每个新 Blog step Logical Run 固定 fast 起步、deep 为 B，无限 AABBAABB。不向任何 LLM 工具 schema 暴露。

每项必须比较实际送到 provider 的 provider-visible bytes，而非 Host timestamp/runtime metadata：

1. 初始 delta 产生 Blogger B；B 只含 Blogger assistant 正文。
2. Blogger busy 时连续写入三 turn：只启动一个 Blogger；空闲后的下一次 delta 覆盖所有跳过内容。
3. 第一次 epoch：coverage 只截断完整 semantic turn；原始 raw tail 保留。
4. 已有 epoch、Blogger busy/failed、新增三 turn、再触发阈值：新 epoch 只能截断 `LastSuccessfulProjection` 已证明覆盖的最长前缀；三 turn 均逐字节留在 raw tail。
5. 同 epoch LatestB 增长与 self-rebase 都不改变 `companion-b-head` 的 ID、role、parts、正文或顺序。
6. 旧/外来 `companion-b-head-*` fixture 不得被视为当前 epoch 的幂等命中；必须 fail closed 或走明确清理路径。
7. 重启后恢复同一个 Blogger；若 Blogger 不存在，发送 full reset，不得将 delta 发给空白 Blogger。

## 4. Fallback 无限 AABBAABB（Logical Run cursor，非 Session 永久 Side）

Fallback 属于 **Logical Run**，不属于 Session 永久状态。A/B 是一对 OpenCode Agent（SelectedAgent / PeerAgent），不是模型槽位。

冻结规则：

1. `session.status=retry` 写 durable cursor advance（唯一写入口）；
2. cursor `Offset ∈ {0,1,2,3}`；`side(0|1)=A=SelectedAgent`，`side(2|3)=B=PeerAgent`；`advance=(Offset+1) mod 4`；
3. 序列永久循环：`A → A → B → B → A → A → B → B → …`；**不存在**因累计 retry 数而产生的 Dead；
4. identity = `logicalRunId + AuthorityRootUserMessageId + providerAttempt`；
5. 新 Authority Root 始终新 cursor：`Offset=0`，Side A = SelectedAgent；
6. 公开创建必须显式 `fast-*` 或 `deep-*`；禁止无前缀旧名称、`build`/`plan`、独立 model override；
7. 发送 Prompt：`Agent=EffectiveAgent`，`Model=None`；模型由 Host 按 `opencode.json` 解析；
8. Continuation / B retry 不写回 LastAuthorityProfile；成功不推进、不重置 cursor。

可接受 canary 轨迹：

```text
Authority Root SelectedAgent=fast-ROLE
  → fail → same-run A (fast) → fail → same-run B (deep)
  → fail → same-run B (deep) → fail → same-run A (fast)   # 第 4 次 retry 回到 A，不死
  → … → 至少证明 12 次 retry 后仍 alive，且 EffectiveAgent 按 modulo-4 循环

Authority Root SelectedAgent=deep-ROLE
  → A=deep, B=fast；同样无限 AABBAABB

new Authority Root（显式另一 Agent）→ 新 cursor Offset=0，Side A = 新 SelectedAgent
```

**发行阻断（12-retry alive）**：必须证明同一 Logical Run 内真实 provider request 轨迹在至少 **12 次** durable retry 后仍继续产生下一 request，且 EffectiveAgent 序列严格为无限 `A A B B A A B B …`。不能用「第四次判死」或「下一真人 prompt 才切 B」冒充同 Run 无限 AABB。若 Host 自身停止 retry，必须用 `ProviderRetryAttempt` continuation 延续同一 Logical Run，否则 No-Go。

**Explicit agents**：所有 Manager/Orchestrator fork、HumanRoot、AgentOwnerRoot 必须显式 Accurate Agent。无前缀 / `build` / `plan` / omit-model 继承 → fail-closed。

## 5. Review witness

真实 Reviewer scenario 必须保留 journal facts 与 Host prompt IDs：

1. 同一 assistant message 内两次 PERFECT：不确认。
2. 两个 provider run，但第二个 root user message 不是 guard confirmation prompt：不确认。
3. 第一 PERFECT → guard prompt accepted → 新 provider run，其 root user message 等于 confirmation prompt ID → 第二 PERFECT 确认。
4. 缺 ProviderRunId：tool 返回错误，不能降级为 ToolCallId。
5. tree 变化、REVISE、rebase（即使 tree hash 相同）均建立新 barrier，要求新的两次 PERFECT。

发布证据应包含 ManagerJob、Reviewer session、barrier、tree、两个 provider run、两个 tool call、confirmation prompt message ID 与第二 root user message ID。

## 6. Inspector 与 Executor

在隔离 Manager worktree 内运行经授权的 Inspector：

- Coder 的 schema 含 Inspector 但不含 Executor；Coder prompt 只将 Inspector 视为不透明调查服务，且不得将其作为常规验证代理。
- Inspector session `Directory` 与 Executor `pwd` 必须等于请求方所在 worktree。
- Inspector schema 精确为 `{read, glob, grep, executor}`，没有 write/edit、fork/join/list、PTY、委派或 verdict。
- 并发两个 Inspector；结果顺序保持与 prompts 顺序一致；不污染 Manager join mailbox。
- 取消请求方后等待 Inspector abort，确认 child session/listener 不遗留。
- 捕获 Parent B snapshot digest；create retry 必须复用同一 snapshot。
- 对相同 `processId|level|start|end`，Executor ID 必须稳定为 SHA-256 派生值。
- map/reduce 乱序完成时，以 chunk index 还原顺序；map/reduce 失败时返回 partial summary、已完成摘要和最后 200KB raw tail，不丢 ProcessResult。
- 输入拒绝 NaN、Infinity、负数；巨大有限 estimate 仅受 cancellation 限制，不能在 int/TimeSpan 转换处溢出。
- Executor Agent 为内部 `fast-executor`/`deep-executor`；新 summary Logical Run 固定 fast 起步，无限 AABBAABB；不向 LLM schema 暴露。

## 7. PTY

仅 DevOps schema 可见 `fork-pty`。Manager 只 `fork(fast-devops|deep-devops)` 委派。逐个验证 `TERM,KILL,INT,HUP,QUIT,USR1,USR2`：

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
8. 只能 fork 显式 Manager Agent（`fast-manager` / `deep-manager`）；无前缀 `manager` fail-closed。

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

当前默认最终分发为**私有交付**：manifest 保持 `private: true`，`license` 为 `SEE LICENSE IN LICENSE`，生成 tarball 但不公开发布到 npm。仅在完成正式许可证与商业授权审查后，才允许将 manifest 改为 `private: false` 并公开发布。最终版本升为 `0.5.0` 前，在新的干净 checkout 重跑整套门禁（含 12-retry alive + explicit-agent-only），不能复用 RC build。证据目录：`docs/evidence/0.5.0/`（见 `0.5.0.md` §21.4）。

## 10. 0.5.0 No-Go（出现任一项不得发布）

```text
仍支持 manager/coder/reviewer 等旧 Agent 名称
仍支持 build 或 plan alias
任意公开创建操作可以省略 fast/deep
万象术仍从环境变量读取模型
万象术发送 Prompt 时仍设置 Model
Authority journal 仍保存 model ID
Fallback 第四次失败仍判死
Fallback 在成功后擅自重置
fast/deep 同角色工具权限不同
fast/deep 使用不同 system prompt
Blogger 或 Executor 名称进入 LLM tool schema
Blogger 不是从 fast-blogger 开始
Executor summary 不是从 fast-executor 开始
重启后 fallback cursor 丢失
重启后 journal 旧 model 覆盖新 opencode.json
Host 收到 Agent 后没有使用该 Agent 对应的模型
12 次 retry 后不再继续物理请求
拼错 Agent 被静默当作新 handle
旧 journal 被猜测性迁移
```
