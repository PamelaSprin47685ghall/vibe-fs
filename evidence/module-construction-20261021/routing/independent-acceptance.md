# routing 0.11.0 独立真实包消费验收

候选 `/private/tmp/wxl-routing-protocol-20261021`，HEAD `d360c50a6833be269665f5b8f3213b7aa107ac4d`，另含 root 原有三个 metadata/inventory 文件。全 22 构建和 standard pack 由另一独立 agent 完成；本轮在其完成后消费明确的实际 tarball，不重建、不改源码或 metadata。

tarball `/private/tmp/vibe-fs-routing-public-pack/wanxiangli-0.11.0.tgz`，SHA256 `687cf81f702a2f229f7924442b9c454d9a96576e61a3ccd819323e0e716d3b93`。包内 54 个成员，版本 0.11.0；本轮在新目录 `/private/tmp/vibe-fs-routing-independent/consumer` 执行真正 npm install tarball，非 workspace link。每个安装文件与打包源对应文件逐字节一致。

## 结果

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 真实 tarball 安装 | exit 0，无运行依赖或编译器 | `install.log` |
| Node22 全公开入口 | 22 imports + 6 组独立行为，通过 | `runtime-node22.log` |
| Bun 全公开入口 | 22 imports + 6 组独立行为，通过 | `runtime-bun.log` |
| 安装包 routing 规则 | Node22 13 pass / 0 fail / 0 skip / 0 TODO，exit 0 | `installed-rules-node22.log` |
| 安装包 routing 规则 | Bun 13 pass / 0 fail，exit 0 | `installed-rules-bun.log` |
| strict TS 正例 | exit 0 | `positive.ts`、`typescript-positive.log` |
| strict TS 负例 | 8 个真实类型错误，TS7 exit 1，无模块解析缺失 | `negative.ts`、`typescript-negative.log` |
| 总体验收入口 | exit 0 | `audit.mjs`、`audit.log` |

Node v22.13.0、Bun 1.3.14。移除 NODE_PATH/NODE_OPTIONS；runtime cwd 为 `/private/tmp`。全部 import 解析到该消费者 node_modules/wanxiangli。安装包无 F# 源码、fsproj、build scripts、归档证据；消费者不存在 Fable、TypeScript、esbuild，resolve 这些编译依赖实际失败。strict TS 使用候选开发工具，未把 compiler 装进消费者，运行时消费不依赖它。

13 条规则是原 `src/routing/tests/002.test.mjs` 正文的临时复制件，仅把相对 dist import 手改成公开 `wanxiangli/routing`。生产测试未改。

## 独立反证范围

- 缺失、null、数字、布尔、字符串、普通对象、Promise 形态的 namespace capacity query 均使 admit 抛错，route 调用计数始终 0；版本缺失/错类型/1/3/NaN/Infinity 同样拒绝。
- query 返回字符串、数字、undefined、null、对象、数组、Promise 或 thenable，均真实调用 query 一次后抛错，没有返回 guessed true、false 或 waiting。route 计数仍 0。非法 role 在 query 执行前拒绝。
- 合法 false 是明确 false，而非无限容量；协议 owner 不暗中调用 route。
- route 恰收 3 个参数；query 恰收 1 个参数。role 被修剪，running 重复项保留。running 数组、逐项 target、previous 都是新冻结副本，实际写入、pop 失败；原件保持可变且原字节/字段不变。
- route 返回 target 是再次严格解码并冻结的新对象；原返回对象未被拿来当 owner DTO。无效输入在 route 前拒绝；Promise/thenable/坏 target 绝不被猜成可用目标；仅 null/undefined 表示 waiting。
- admit 交还冻结 policy，保留用户函数引用，既不执行用户 query，也不执行 route。
- `ollama-cloud/gemma4:31b:none` 按最后冒号还原 tagged model，providerOf 一致；缺 reasoning 仍拒绝。

## 边界

这是库 scope 的实际发行消费证明。它不证明主仓 loader 的文件加载、scheduler cache、真实 admission 在非法 query 时不入队/不替换 incumbent；那些需要 root 的 gen104 主仓 requirement 验收。库不持有 queue、capacity ledger、Host SDK，也未新增这些职责。

上一 agent 的独立全 22 构建/standard inventory/21 旧模块连续性结果见 `/private/tmp/vibe-fs-routing-overlay/receipt.md` 与 `/private/tmp/vibe-fs-routing-continuity.json`：仅生成 PID 注释归一，21 个原 JS 相同；21 个声明原字节相同。本轮没有把归一比较写成原字节相同，也没有重跑旧 21 模块所有行为套件。

既有 P2：如果 model 和 reasoning 都含冒号，未转义 durable string 无法唯一逆向解码；最后冒号规则修复 tagged model + 无冒号 reasoning 的真实情形，没有证明任意 target round trip。本轮不扩展格式或迁移历史数据。

没有新阻断。原环境失败日志未覆盖。候选结束仍只有 root 原有三文件 dirty，`git diff --check` exit 0。全部本轮命令已返回，未启动后台消费者。

metadata SHA256：

- package.json `05a897aca1e8cc4ed855e45bc70d4fd738a9b2841575bcd60cb3cb92422600e6`
- package-lock.json `6cc535db0e92f0d7422e6a21146ceb7cc108f26d0b8ba4970adb68c3b2020b28`
- scripts/tests/exports-package.test.mjs `a664c5ab1f3a1b8d0be847d2cdec0e62c42fdc3cbdc7488460850ee75426e5df`
