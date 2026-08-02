# Post-Anneal-2 证据 — 验证层恢复到 mjs 单一入口

本目录保存退火二（休克三 / 包 T 完成）时的完整机器反馈。

采集时间：2026-07-30T15:19+08:00
绑定 commit：`2b30301c`（分支 `refactor/ssot-shock-anneal`）
环境：Node v26.4.0、.NET 10.0.110、Linux 7.1.4 x86_64、本机 TZ=CST

## 与前两次采集的关系

| 目录 | 性质 |
|------|------|
| `pre-shock/` | 旧世界最后一次完整反馈。用于判断哪些失败是迁移引入的 |
| `post-freeze/` | 封炉工装自证：静态检查器能跑、能测出预期残留 |
| `post-anneal2/` | 本次。第 0–2 层反馈全部恢复，且验证层已换语言 |

`post-freeze` 采集时 `tests-next` 仍是唯一测试入口（292 个测试，1 个失败）。
本次 `tests-next` 已整体删除，`test:mjs` 是唯一的第 1–2 层入口。

## 文件

| 文件 | 内容 |
|------|------|
| `COMMIT.txt` `GIT-STATUS.txt` `environment.txt` | 采集点绑定 |
| `build-production.txt` | `dotnet build next/` 完整输出 |
| `build-fable.txt` | `npm run build`（Fable precompile + postbuild） |
| `test-mjs.txt` | `test:mjs` 在三个时区下各跑一次的完整输出 |
| `module-load.txt` | 172 个发射模块逐个 import 的链接检查 |
| `test-inventory.txt` | 每个测试文件的测试数 |
| `ssot-lint.txt` `architecture-gate.txt` `strip-doc-bold.txt` | 第 0 层三件套 |
| `shock-audit.txt` | 旧符号灭绝表 + 单一写入口 + SHOCK 标记 |

## 结果摘要

```text
dotnet build          0 error / 0 warning
npm run build         174 源 → 172 发射模块
test:mjs              386 passed / 0 failed（UTC / Asia-Shanghai / America-New_York）
module load           172/172 可加载
ssot-lint             OK — 136 条款，387 处引用，14 个文件
architecture-gate     OK — 174 production + 24 test files
strip-doc-bold        clean
shock-audit           八个单一写入口 ok (1)；SHOCK-UNMIGRATED 0
```

## 为什么三个时区各跑一次

`PERSIST-001` 要求 journal 行的字节不依赖写入机器。包 T-2 实测发现
`Envelope.serialize` 会渲染读者的本地时区偏移：`Encode.Auto` 直接编码
`DateTimeOffset`，而 `Decode.Auto` 解码时挂上读者本地 offset，所以「读一行再写回」
在 `TZ=Asia/Shanghai` 上把同一时刻渲染成 `+08:00`。

单时区跑不出这个缺陷——UTC 机器上往返完全稳定。因此三时区成为常规采集项，
而不是一次性排查手段。

## 测试分布（386）

```text
Context/          187   SSOT/12 失败驱动上下文恢复（包 X1–X8 随包写）
Fallback/          32   SSOT/04
Execution/         24   SSOT/09
Review/            23   SSOT/05
domain.meta        22   facade 自身契约（四个静默陷阱 + 全量 undefined 扫描）
Prompt/            21   SSOT/03
Orchestrator/      20   SSOT/06
Journal/           35   SSOT/11（envelope 17 第 1 层 + boot 18 第 2 层）
Kernel/            12   ARCH-009
guide-contract     10   VERIFY-005/008 生产入口可达性
```

`Journal/boot.test.mjs` 是唯一的第 2 层（资源契约）文件：`JournalWriter` 与 `Boot`
是唯一触碰真实文件系统的领域模块，而 PERSIST-004 问的是「半写的文件在启动时怎么办」，
用内存替身断言只会断言替身。

## 本次采集暴露并修正的生产缺陷

四处，全部由「写测试时真的观察产物」发现，无一能被 `dotnet build` 看到：

| 缺陷 | 条款 | 表现 |
|------|------|------|
| `Envelope.serialize` 渲染本地时区偏移 | PERSIST-001 | 两台不同时区机器对同一历史产出不同字节 |
| journal 权限位从未设定 | PERSIST-006 | umask 022 下是 755/644，条款要求 700/600 |
| `Task.CompletedTask` 使五个模块无法加载 | VERIFY-008 | 含 `package.json` 入口；装得上、import 即抛 SyntaxError |
| `createJob` 无条件覆盖 | ORCH-006 | 重放创建事实把 `Published` 重置为 `ManagerStarted` |

第三处最值得记档：`Task.CompletedTask` 在 .NET 下编译通过，Fable 为它发射
`get_CompletedTask`，而 `fable-library-js` 不导出这个名字。`dotnet build` 只检查 F#，
`test:mjs` 只 import `domain.mjs` 绑定的模块（facade 从不 import `OpenCode/*`，实测 0 处），
两侧都没有理由碰到入口模块。`module-load.txt` 这一项就是为关掉这个缺口而加的。

## 本目录不采集什么

不采集 canary / E2E / `test:release` / `gate-testkit`。剧本 fixture 即将被包 K 整体
重写为 TOML，采集旧结果不产生可用于新世界的判据。这四项在退火三采集。

`test:manager-tools` 当时也未采集，但本目录给出的理由是错的：它写「读 testkit 的 mock
森林」，实测该测试唯一的项目 import 是 `build/next/OpenCode/SpikePlugin.js`，既无 mock
provider 也无 HTTP、端口、HOME/XDG 隔离，只需 `git init` 到临时目录。它是放错目录的第 2 层
资源契约测试，与剧本森林重写无关。真正该说的理由是：它当时已经是红的（见下）。

该测试已于包 K 迁入 `tests-mjs/Plugin/manager-tool-contract.test.mjs`，`test:manager-tools`
随之删除，覆盖面并入 `test:mjs`。
