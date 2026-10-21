# MF01 长 park 原语实验：用户提前中止，非一小时通过

用户要求保存后暂停，因此本实验提前恢复并关闭。没有继续等待一小时，没有改主仓源码、index 或门禁，没有部署或启动后续测试。

## 实际结果

- STOP 首次确认：2026-10-21T00:37:44.700Z（SGT 08:37:44.700）。
- 用户暂停要求处理并保存快照：2026-10-21T00:57:03.697Z。
- 提前 CONT：2026-10-21T00:57:30.550Z；停止实际 UTC 差值 1,185,850ms，即 **19分45.850秒**。不是一小时。
- 原目标 PID/PGID 29066，PPID 为 harness 29064，birth `Wed Oct 21 08:37:44 2026`，nonce `9d719c85-cd7e-41ac-ab85-381baae93b31`。
- 20 份周期暂停样本均为 `Ts`；累计 CPU 始终 `0:00.05`（ps 显示精度）；数字 FD 集合始终是同一 15 个；RSS 观测范围 4096–43024KiB。RSS 下降不被解释为 guest 执行了分配或释放。
- 暂停后只写入一次 76-byte 消息并结束输入。`Written` 仅记为第一跳回执；暂停期间没有应用回执。
- CONT 后 00:57:30.577Z，**同一 PID、同一 nonce、同一 async closure** 返回 keep 41→42，并确认唯一消息 `messageCount=1`，内容和 ID 原样；没有重启或重建 continuation。
- ProcessSession 捕获原生 Closed，exit `{code:0,signal:null}`、failure=null、stderr 0 bytes。目标已正常退出后，才对已核验 harness 发送 TERM 取消余下的一小时计时。
- 00:57:30.623Z 已确认目标 29066 和 harness 29064 均 ESRCH。42 个 harness 创建的 ps/lsof observer 在各自返回时均已 ESRCH；提前收尾脚本额外创建的 ps 也逐个验证已退出。没有信号发给无关进程。
- 原长计时 harness 因显式用户取消诚实返回 exit 1 / AbortError / phase Failed；该原始状态和日志没有改成通过。提前收尾控制脚本 exit 0，权威中止 receipt 明确 `oneHourCompleted=false`。exec session30667 已结束。

## 保存的证据与源码

全部路径前缀 `/private/tmp/vibe-fs-mf01-long-park`：

- `.mjs`：完整长计时 harness，硬编码 3600000ms 暂停、3630000ms backstop、60s 周期观测。
- `-child.mjs`：保留闭包和 async continuation 的真实目标程序。
- `-user-stop.mjs`：中止控制脚本，先核对原目标 pid/ppid/pgid/birth 和 harness command，再 CONT→唯一回执→目标 ESRCH→harness TERM→两 PID ESRCH；不接收任意 PID。
- `-samples.jsonl`、`.log`：完整实际原始事件/样本。
- `-stopped-before-user-cont.jsonl`：CONT 前保存的完整暂停样本前缀，未被后续恢复日志替换。
- `-state.json`：原 harness 最终状态，保留用户取消的 AbortError 与 nativeClosed/cleanup。
- `-owned.jsonl`：目标身份和全部短 observer 的退出账。
- `-user-stop.json`：用户中止处理时刻、原 Stopped 状态、CONT 时刻、回执、全部清理结果和最终 owner 状态。
- `-user-stop.log`：提前收尾脚本成功结果。

请将上面三个 `.mjs` **源码**及本报告显式保存到仓内证据目录；不能只保留这些临时路径。该保存由 root 操作，本 agent 未写主仓。

源码 SHA256：

- harness：`6ec83a76d87913625fc1a3e46a75a6ae40827c0d9e4ae487f813d6f26f3e6bc6`
- child：`6103722f6bd3aa2700e75d3b87f43369735a396c5f89ae37ffb8ba2da69b1b08`
- user-stop：`edd4a715a4b8cade3a3b8284580df39128cf6ade9829945a5c58a4582fa1e523`

## 固定依赖和平台

实际长实验只运行 **Node v22.13.0，macOS 26.6.2 / 25G83，Darwin25.6.0 arm64**。Bun1.3.14 在系统中存在，但本次长实验没有运行 Bun 目标，不能把此前短原语的 Bun 绿色移作此次结果。

Node executable `/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node` SHA256：`5c79f8e198fd2456ea6660aaa6be218f9f44747f2306c7d0f5f97b087b2f2dfc`。

harness 仅从 `/Users/yuanxi/.codex/worktrees/upstream-plan-20261020/vibe-fs/wanxiangli/dist/process/ProcessSession.js` 导入公开 ProcessSession class，使用 started/Capture/write/endInput/inspect/wait/discardOutput 和失败清理 signal；STOP/CONT 明确由父端 Raw POSIX signal 发给核验后的自建 PID。公共 SignalName 本身不含 STOP/CONT，本实验没有给生产接口加此能力。

包版本为 wanxiangli0.11.0；停止时库 HEAD `45849f51778f6a12dcafbc12ce1c8c7f7f65502b`。对应 source 依赖可以重建 process，不能依赖临时 node_modules 永久存在。固定元数据：

- package.json SHA256 `05a897aca1e8cc4ed855e45bc70d4fd738a9b2841575bcd60cb3cb92422600e6`
- package-lock.json SHA256 `6cc535db0e92f0d7422e6a21146ceb7cc108f26d0b8ba4970adb68c3b2020b28`
- process 声明 SHA256 `6a3104eac46da34bb6dc7f568735a16a5863c408c9eb61660e23968d91d8badd`
- 开发工具锁定 Fable compiler-js3.8.1、library-js2.8.1、esbuild0.28.2、TypeScript7.0.2；不使用 dotnet build。

原语启动后记录的 process bundle SHA256 为 `3560d83d4dffa88c3770aa791c80fff99e3ed3eee894b27dc58642fdbc288b47`。实验期间 root 正常重建库，该磁盘路径结束时 SHA256 为 `1881da8fa1c8bab785100122ce51fa2aae3a48b3d7c4c6753cb515391117ab11`。本进程已加载的模块没有重新 import；原始 bundle 字节未另行保存，不能把结束文件 hash 当作启动文件 hash，亦不能声称从一个 hash 已证明两份字节相同。从已发布 permit 基线9c88393到结束 HEAD，process/native-process 源码与 build 脚本未变。

可用于将来干净消费者重装的独立发行包 `/private/tmp/vibe-fs-routing-public-pack/wanxiangli-0.11.0.tgz` SHA256 为 `687cf81f702a2f229f7924442b9c454d9a96576e61a3ccd819323e0e716d3b93`；其 process JS 为 `57ecdb37c1c7c00cca0b735853fb563c85b42efd9ca3290261a332cd0f199054`。这是经过独立消费验收的同源发行物，不冒充本次已加载 bundle 的原始字节。

## 完整复跑说明（只供以后显式恢复后执行）

**当前已暂停，不执行以下命令。** 本 harness 使用固定 `/private/tmp` 文件名，先归档旧输出，避免 append 混合两个 nonce。下例假定 root 将三个源码按原文件名保存到 `evidence/module-construction-20261021/park-interrupted/`；若保存位置不同，只调整 `park_sources`。

```sh
park_repo=/Users/yuanxi/.codex/worktrees/upstream-plan-20261020/vibe-fs
park_sources="$park_repo/evidence/module-construction-20261021/park-interrupted"
park_node=/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node
park_archive="$(mktemp -d /private/tmp/vibe-fs-mf01-long-park-prior.XXXXXX)"
for park_suffix in .log -samples.jsonl -state.json -owned.jsonl -stopped-before-user-cont.jsonl -user-stop.json -user-stop.log; do
  park_file="/private/tmp/vibe-fs-mf01-long-park$park_suffix"
  if [ -e "$park_file" ]; then mv "$park_file" "$park_archive/"; fi
done
cp "$park_sources/vibe-fs-mf01-long-park.mjs" /private/tmp/vibe-fs-mf01-long-park.mjs
cp "$park_sources/vibe-fs-mf01-long-park-child.mjs" /private/tmp/vibe-fs-mf01-long-park-child.mjs
cp "$park_sources/vibe-fs-mf01-long-park-user-stop.mjs" /private/tmp/vibe-fs-mf01-long-park-user-stop.mjs
"$park_node" --version
"$park_node" --check /private/tmp/vibe-fs-mf01-long-park.mjs
"$park_node" --check /private/tmp/vibe-fs-mf01-long-park-child.mjs
"$park_node" /private/tmp/vibe-fs-mf01-long-park.mjs > /private/tmp/vibe-fs-mf01-long-park.log 2>&1
```

若 Node 临时目录已清理，须先恢复上列固定版本及 hash，或显式记录新执行文件；若 library dist/node_modules 已清理，须在固定源码和锁文件上 `npm ci` 后 `node scripts/build.mjs process` 重建，记录新的生成物 hash。只能在已恢复授权后做这些准备。原 harness 的 import 是上列绝对 checkout 路径；换 checkout/机器必须手工改该一处 import 并重新记录 harness hash，不能默默源码回退。

本实验没有依赖 permit/routing 的 `/private/tmp/*/consumer`。这些目录若已清理，要复跑其各自发行证明须重新实际 tarball install 并恢复其独立 audit/TS 消费文件；它们不属于本次长 park 复跑前提。若改为全新 public package consumer 运行本 harness，则须实际安装上列固定 tarball，将 import 明确手工改成该消费者能解析的 `wanxiangli/process`，记录变更；不使用 workspace link 或自动 fallback。

未来若再次需要提前结束，必须**先创建本次**中止 receipt 与 Stopped 前缀（不能复用旧 receipt），再运行固定的 user-stop 控制脚本：

```sh
"$park_node" --input-type=module <<'JS'
import { readFileSync, writeFileSync, copyFileSync } from 'node:fs'
const base = '/private/tmp/vibe-fs-mf01-long-park'
const state = JSON.parse(readFileSync(base + '-state.json', 'utf8'))
if (state.phase !== 'Stopped') throw Error('current owned target is not Stopped')
copyFileSync(base + '-samples.jsonl', base + '-stopped-before-user-cont.jsonl')
writeFileSync(base + '-user-stop.json', JSON.stringify({
  reason: 'User requested save and pause; end early without claiming one-hour pass',
  handledAt: new Date().toISOString(), originalStoppedState: state,
}, null, 2))
JS
"$park_node" /private/tmp/vibe-fs-mf01-long-park-user-stop.mjs > /private/tmp/vibe-fs-mf01-long-park-user-stop.log 2>&1
```

STOP/CONT 前须核对自建目标完整身份；取消 owner 前必须核对其 command，不能套用陈旧 PID。正常所有进程由原 ProcessSession 回收；失败只对它拥有的组发 KILL并等待。不得用这些脚本操作其他进程。

## 限制与交接

只获得约20分钟的物理暂停、单个76-byte消息及原 continuation 恢复证据。一小时目标被用户中止，保持未验。没有完整 sandbox API、同步/异步 RPC、Host副作用、deadline借用、abandon finally、任意大消息背压或脱组后代清理证明。MF01仍未接收。

所有证据文件保留；原目标和 owner 均已退出，不存在需后续唤醒的停住进程。报告交接后停止工作，不再重跑、补测或变更源码。
