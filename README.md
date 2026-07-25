# 万象术

OpenCode Agent DSL 插件。模型侧工具面由角色静态装配；实现侧使用 F# Structured Flow、per-runtime NDJSON 领域事实、completion mailbox、Companion 投影和 Git 发布端口。

产品语义：[`next/Doc/SSOT.md`](next/Doc/SSOT.md)。工程执行状态与纠偏顺序：[`AGENTS.md`](AGENTS.md)。

## 当前边界

当前分支已回滚测试驱动的生产污染：没有生产进程台账、全局进程 kill、传输层 prompt 重试/伪造失败、fail-open Git tree 或伪 PTY 工具面。

直接验证过：

```text
npm run build
npm run test:manager-tools
node testkit/opencode/tests/gate-testkit.mjs
npm run test:e2e:p0
```

TestKit 以 scenario/session/role/turn/request-kind lane 匹配真实 OpenCode session/parent headers；title、synthetic continuation 与 Blogger 均为显式 expectation。Manager→Coder→Join 使用 child、write、terminal、join、terminal 的真实 Host barrier；Companion 成功回合原子持久完整 B 与 projection，replacement restart canary 验证 B 恢复与 raw tail。P0 保持并行，最多重复 3 次；每个场景使用 1s causal-progress Watchdog。500 provider retry 会写入 durable failure fact，且 restart canary 验证其累计；上述不构成 release 资格，也不证明 Fallback A/A/B/B、ReviewGuard finish、PTY fork surface 或 Orchestrator Git 发布。

## 角色模型

```text
Orchestrator (fork/join)
  └── Manager (fork/join/list)
        ├── Coder
        ├── Inspector (executor)
        ├── Browser
        ├── Meditator
        └── Reviewer (PERFECT/REVISE)
```

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
