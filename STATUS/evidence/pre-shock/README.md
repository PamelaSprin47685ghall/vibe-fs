# Pre-Shock 基线 — 休克迁移开始前的最后一次完整机器反馈

本目录是 shock-anneal 迁移的 封炉基线。休克期不再运行编译和测试，
因此这里保存的是"旧世界最后一次自我陈述"。

## 用途

不是为了在休克期比较绿灯，而是为了以后回答三个问题：

1. 哪些失败是休克前就存在的（不是迁移引入的）
2. 哪些行为是迁移引入的
3. 哪些绿灯证据已经不适用于新语义

## 采集时间与身份

见 `COMMIT.txt`、`environment.txt`。

## 结果摘要

| 通道 | 结果 |
|------|------|
| `dotnet build next/Wanxiangshu.Next.fsproj` | 通过（0 error / 0 warning） |
| `dotnet build tests-next/Wanxiangshu.Next.Tests.fsproj` | 通过（0 error / 0 warning） |
| `npm run build`（Fable production） | 通过 |
| `npm run test:compile`（Fable tests） | 通过 |
| `npm run test:next` | 290 passed / 3 failed / 293 total |
| `node testkit/opencode/tests/gate-testkit.mjs` | 29 passed / 0 failed |

### 3 个已存在的单元测试失败

| 测试 | 性质 | 是否新语义相关 |
|------|------|--------------|
| `ArchitectureGates > Next_source_files_do_not_exceed_300_lines` | 13 个文件超过 260 行警告线 | 无关（文件尺寸） |
| `ArchitectureGates17 > TASK_section_17_semantic_gates` | `next/OpenCode/TurnCompletionProgram.fs` 331 行，硬失败 >300 | 无关（文件尺寸） |
| `ReviewRequirementBoundaryTests > Confirmed reviewer terminal resets only previously reviewed human requirements` | Assert.equal 失败 | 相关（Review 语义） |

## 关键判断：为什么仍然需要休克迁移

基线是绿的，但绿灯正在保护旧语义。

`STATUS/conformance.md` 同时记录：

```text
PROMPT-007   CONTRADICTS      多处直接 PostPromptFireAndForget
FALLBACK-003 CONTRADICTS      ProviderFailureWakeup 直接写 durable cursor
FALLBACK-005 NOT_IMPLEMENTED  尚无 12 attempt 上限
REVIEW-003   NOT_IMPLEMENTED  仍使用 same-root 和 physical-message 两种弱代理
ORCH-005     CONTRADICTS      锁持有跨 review 期间
COMPANION-001 CONTRADICTS     从 Agent 字符串解析角色
```

290 个通过的测试没有发现这些矛盾，因为它们断言的是当前实现，
不是 SSOT 条款。这正是休克法适用的形态：机器反馈是可信的，但它证明的
命题是错的。

因此休克期关闭反馈通道，代价不是"失去正确性保证"——当前的绿灯本来
就不保证 SSOT 合规。

## 陈旧记录更正

`STATUS/00-current.md` 声称 `285 passed / 19 failed`。实际为
`290 passed / 3 failed`。该文件在休克期开始时已过时，其"当前阻断"
一节描述的是更早的仓库状态。

## 文件

| 文件 | 内容 |
|------|------|
| `COMMIT.txt` | 基线 commit + 分支 |
| `GIT-STATUS.txt` | 基线工作树状态 |
| `environment.txt` | node / npm / dotnet / kernel 版本 |
| `build-production.txt` | `dotnet build` 生产工程完整输出 |
| `build-tests.txt` | `dotnet build` 测试工程完整输出 |
| `unit-baseline.txt` | Fable build + test:compile + test:next + gate-testkit 完整输出 |

未采集 canary / E2E / `test:release`：这些需要真实 Host 与长时间运行，
且其 fixture 即将被新语义整体重写，采集旧结果不产生可用于新世界的判据。
`gate-testkit` 已采集，因为它验证的是 mock 森林与隔离机制本身，
在退火三仍然是第一层门禁。
