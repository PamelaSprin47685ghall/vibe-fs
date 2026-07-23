# 万象术

OpenCode Agent DSL 多代理插件。模型侧只有 `fork` / `join` / `list` / `verdict` 等极小工具面；实现侧是 F# Structured Flow、Per-Runtime NDJSON 事实日志、Completion Mailbox、Companion 投影与 Git 发布。

## 产品模型

```text
Orchestrator
  └── Manager (fork/join/list)
        ├── Coder + Companion Blogger
        ├── Inspector (exec only)
        ├── Browser
        ├── Meditator
        └── Reviewer (PERFECT/REVISE)

Companion:
  X 的 B 版工作记录 + 前缀替换，Y 忙时跳过不阻塞

Process:
  3×estimate 唯一 deadline + 完整输出 spool + 200KB map/reduce 摘要

ReviewGuard:
  同一 Git tree 连续两次 PERFECT 才确认

Journal:
  只持久化跨重启领域事实，不持久化 Stage/Phase/Lease/Owner
```

## 安装与入口

构建产物是 npm 包 `wanxiangshu`，OpenCode 生产入口：

```text
wanxiangshu
→ build/OpenCode/Plugin.js
```

```bash
npm run build
```

默认 `main` / `exports["."]` 都指向 `build/OpenCode/Plugin.js`。不再导出 Mux/OMP/万象阵旧入口。

## 命令

| 命令 | 作用 |
| --- | --- |
| `npm run build` | Fable 编译 `next/` 到 `build/` |
| `npm test` | 编译 `tests-next` + 跑 TestKit gate |
| `npm run test:e2e:p0` | Manager DSL 20× canary |

## 开发布局

```text
next/                 Agent DSL 生产源码
tests-next/           新架构测试
testkit/opencode/     独立 OpenCode harness
AGENTS.md             唯一产品宪法
MIGRATION.md          行为总账
```

旧 `src/` / `tests/` / Mux / OMP / 万象阵实现已冻结，不作为生产路径。

## 不变式

1. 上下文压缩 = `B` + watermark 后的 Raw Tail。
2. Blogger 失败/延迟不阻塞主会话。
3. 控制流看强类型 DU 与 Git Hash，不看聊天文本。
4. Busy agent 不隐式排队。
5. Process 只有一个 `3×estimate` deadline。
6. 业务失败走 DU，不抛伪装异常。
7. Completion 先入邮箱再消费。
8. 双 PERFECT 绑定 Git Tree Hash。
9. Orchestrator 发布串行。
10. 禁止 Stage/Phase/Lease/Owner 复合状态机。
