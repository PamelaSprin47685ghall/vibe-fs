# 万象术

OpenCode Agent DSL 插件。模型侧工具面由角色静态装配；实现侧使用 F# Structured Flow、per-runtime NDJSON 领域事实、completion mailbox、Companion 投影和 Git 发布端口。

当前版本：`0.5.0-rc.1`（OpenCode Agent DSL） — 唯一产品语义见 [`SSOT/00.md`](SSOT/00.md)。模型由 OpenCode `agent` inventory 解析；公开身份必须是精确的 `fast-*` / `deep-*`。

产品语义：[`SSOT/00.md`](SSOT/00.md)。实现状态与合规表：[`STATUS/conformance.md`](STATUS/conformance.md)。当前迁移总账：[`STATUS/shock-anneal.md`](STATUS/shock-anneal.md)。

## 当前边界

本仓库正在进行 SSOT 休克-退火迁移。 生产代码与测试正在从旧语义整体迁移到 `SSOT/` 条款。迁移期间编译与测试反馈按阶段关闭与恢复；当前进度、每个工作包状态、旧符号灭绝表见 [`STATUS/shock-anneal.md`](STATUS/shock-anneal.md)。

休克开始前的最后一次完整机器反馈保存在 [`STATUS/evidence/pre-shock/`](STATUS/evidence/pre-shock/)。该基线的绿灯证明的是旧实现，不是 SSOT 合规——`STATUS/conformance.md` 同时记录多个中心协议处于 `CONTRADICTS` 或 `NOT_IMPLEMENTED`。

TestKit 以 `ProviderSemanticProjection` 完整前缀匹配确定性剧本边；同一前缀幂等返回同一响应，分叉只能来自不同可见 user 内容（VERIFY-003、VERIFY-007）。P0 保持并行，release gate 恰好 3 轮；每个场景使用 2 秒 causal-progress Watchdog（VERIFY-004）。

## 角色模型

```text
Orchestrator (fork/join)
  └── Manager (fork/join/list)
        ├── Coder (file tools + opaque inspector)
        ├── Inspector (read/glob/grep/executor)
        ├── DevOps (fork-pty/executor/read/glob/grep/inspector/coder/join/list)
        ├── Browser
        ├── Meditator
        └── Reviewer (PERFECT/REVISE)
```

Coder 可读写代码并调用不透明的 `inspector` 调查具体必要的事实；其 schema 不含 `executor` 或终端能力，Coder prompt 也不暴露 Inspector 的内部执行权限。Coder 不应把 Inspector 当作常规验证代理，所需测试仍交接给 DevOps 或 Reviewer。

DevOps 是终端操作员：独占 `fork-pty`，可 `executor`，可用 `read/glob/grep/inspector` 取证，文件修改只能通过同步 `coder` 工具委派，禁止直接 write/edit。

Companion Blogger 仅是认知上下文；它不能决定调度、Review、Git 或进程事实。角色和精确权限以 SSOT 为准。

## 核心不变量

条款 ID 为规范位置；此处只是索引，冲突时以 `SSOT/` 为准。

1. Busy existing agent 的 `fork` 是同 child fire-and-forget nudge；不得创建 prompt queue（EXEC-002）。
2. completion 先写 mailbox；`join()` 消费任意可用 completion，消费后写 `HandleRetired` tombstone（EXEC-004、EXEC-009）。
3. 进程 deadline = `min(3 × estimated_running_secs, 管理员 hard limit)`；进程资源只由拥有者清理（EXEC-011）。
4. Review 必须由同一 Git tree 的两个不同 ProviderRunIdentity / ToolCallId `PERFECT` 确认，且第二次的 provider input seal 必须证明包含第一次 challenge（REVIEW-003、REVIEW-010）。
5. Fallback 属于 Logical Run。Offset 按 A/A/B/B 无界循环，成功不重置 Offset 只清零 `ConsecutiveFailureCount`；自动恢复预算默认 12 连续失败后写 `FallbackExhausted`（FALLBACK-002、FALLBACK-004、FALLBACK-005）。
6. Host 事件只负责唤醒；唯一的 `FallbackCursorAdvanced` 写入口是 FallbackController（FALLBACK-003）。
7. 插件 Prompt 必须经 PromptDispatcher；发送时 `Model=None`，未知来源 fail-closed（PROMPT-005、PROMPT-006）。
8. Git tree 读取失败必须 fail closed（ORCH-008）。
9. PTY completion 只由 backend `onExit` 触发；Signal/Close 不提前完成（EXEC-015）。

## 构建与测试

```bash
npm run build
npm run test:compile
npm run test:next
npm run test:manager-tools
node testkit/opencode/tests/gate-testkit.mjs
npm run test:e2e:p0:three
```

先运行当前改动的最小目标测试；只有该阶段的契约已证明后才运行更广的套件。TestKit 每个 scenario 必须独占 workspace、HOME/XDG、Provider、端口、Journal、spool、进程组、diagnostics 和 expectation store。

Journal 位于 Git common directory 的私有 `wanxiangshu-next/runtimes` 路径；TestKit 不在受测 workspace 创建 `node_modules` 或 `.wanxiangshu-next`。

## 生产入口

```text
wanxiangshu
→ build/next/OpenCode/Plugin.js
```

`package.json` 的 `main` 与 `exports["."]` 指向该入口。

## 开发布局

```text
next/                 生产 Agent DSL
tests-next/           Fable contract/Port tests
testkit/opencode/     独立 OpenCode harness
SSOT/                 产品语义（唯一规范，条款 ID 寻址）
STATUS/               实现状态、合规表、迁移总账
```

旧实现不作为生产依赖。历史代码仅可作逐符号行为证据；禁止整版本 checkout 或无审查覆盖。
