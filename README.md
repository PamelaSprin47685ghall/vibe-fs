# 万象术

OpenCode Agent DSL 插件。模型侧工具面由角色静态装配；实现侧使用 F# Structured Flow、per-runtime NDJSON 领域事实、completion mailbox、Companion 投影和 Git 发布端口。

**当前版本：`0.4.0`（私有最终版）** — 见 [`docs/RELEASE-0.4.0.md`](docs/RELEASE-0.4.0.md)、[`docs/evidence/0.4.0/`](docs/evidence/0.4.0/)。包保持 `private: true`，按 `LICENSE` 仅限书面商业协议分发。

产品语义：[`next/Doc/SSOT.md`](next/Doc/SSOT.md)。工程执行状态与纠偏顺序：[`AGENTS.md`](AGENTS.md)。

## 当前边界

当前分支已回滚测试驱动的生产污染：没有生产进程台账、全局进程 kill、传输层 prompt 重试/伪造失败、fail-open Git tree 或伪 PTY 工具面。

直接验证过：

```text
npm run build
npm run test:manager-tools
node testkit/opencode/tests/gate-testkit.mjs
npm run test:e2e:p0
npm run test:e2e:p0:parallel
```

TestKit 以 scenario/session/role/turn/request-kind lane 匹配真实 OpenCode session/parent headers；每个实际产生的 title、zero-width continuation、Blogger、Reviewer 请求均须显式 expectation。Manager→Coder→Join 使用 child、write、terminal、join、terminal 的真实 Host barrier；Companion 成功回合原子持久完整 B 与 projection，replacement restart canary 验证 B 恢复与 raw tail。`HostEventRouter` 用 terminal 前的 text/tool part 还原 assistant，并在未确认的 Manager terminal 以 listener-before-send 发送 durable ReviewGuard；Reviewer 无 verdict 同样 nudge，abort terminal 不发送 guard 或 continuation。P0 保持并行，最多重复 3 次；每个场景使用 2 秒 causal-progress Watchdog（仅接受阻塞 expectation 消费、显式 session/restart/barrier；background Blogger 与普通 SSE/HTTP 不延长它）。500 provider retry 会写入 durable failure fact，且 restart canary 验证其累计；上述不构成 release 资格，也不证明 Fallback A/A/B/B、PTY fork surface 或 Orchestrator Git 发布。

## 角色模型

```text
Orchestrator (fork/join)
  └── Manager (fork/join/list)
        ├── Coder (file tools + opaque inspector)
        ├── Inspector (executor)
        ├── DevOps (fork-pty/executor/read/glob/grep/inspector/coder/join/list)
        ├── Browser
        ├── Meditator
        └── Reviewer (PERFECT/REVISE)
```

Coder 可读写代码并调用不透明的 `inspector` 调查具体必要的事实；其 schema 不含 `executor` 或终端能力，Coder prompt 也不暴露 Inspector 的内部执行权限。Coder 不应把 Inspector 当作常规验证代理，所需测试仍交接给 DevOps 或 Reviewer。

DevOps 是终端操作员：独占 `fork-pty`，可 `executor`，可用 `read/glob/grep/inspector` 取证，文件修改只能通过同步 `coder` 工具委派，禁止直接 write/edit。

Companion Blogger 仅是认知上下文；它不能决定调度、Review、Git 或进程事实。角色和精确权限以 SSOT 为准。

## 核心不变量

1. Busy existing agent 的 `fork` 是同 child fire-and-forget nudge；不得创建 prompt queue。
2. completion 先写 mailbox；`join()` 消费任意可用 completion。
3. 进程只有一个 `3 × estimated_running_secs` deadline；进程资源只由拥有者清理。
4. Review 必须同 Git tree 的连续两次 `PERFECT`。
5. Fallback 是每 session 累计 A/A/B/B/Dead；Transport 不得归因。
6. Git tree 读取失败必须 fail closed。
7. 真实 PTY 未接入前，不得把 shell command 宣称为 PTY。

## 构建与测试

```bash
npm run build
npm run test:compile
npm run test:next
npm run test:manager-tools
node testkit/opencode/tests/gate-testkit.mjs
npm run test:e2e:p0
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
 next/Doc/SSOT.md      产品语义
 AGENTS.md             工程纪律与当前证据
 MIGRATION.md          行为迁移总账
```

旧实现不作为生产依赖。历史代码仅可作逐符号行为证据；禁止整版本 checkout 或无审查覆盖。
