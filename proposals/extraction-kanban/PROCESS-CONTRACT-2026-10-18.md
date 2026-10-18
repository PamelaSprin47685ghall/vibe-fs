# CUT-02-R6-A：纯契约、实际注入与编译边界

输入主仓 `57ba194b6`，库 `10bbb32 / 0.6.4`。本包不改库、vendor、持久协议、准入仲裁或物理终态规则。B 的总预算和 C 的运行时适配分别交付。

## 实现

`process-processrequest` 现在只编译 ProcessRequest 与 PtyTypes 的 fs/fsi，共四个文件，无工程依赖。请求、结果、错误、`RunProcess`、`RecoverPty` 和 PTY 词汇不带物理对象、完成源、能力实例或工厂。包含完成源的 ReadPlan 移到实际 Pty 运行时；其余物理代码由新的 `process-runtime` 工程编译。纯契约不再通过工程边带入它们。

删除单次 Command 中无人消费的 PtyOptions 和旧类型；持续终端继续使用自己的 PtyCommand。run 工具和两条 Git 路径接收 RunProcess，由 ToolRegistry、Change Host 和独立 Git Hook 组合真实 ProcessRunner。旧 `GitGateway.createDefaultRunner`、`OrchestratorGit.run` 退出，没有保留转发别名。

HostForkRuntime 必须显式取得端口 option 与 RecoverPty。没有端口表示没有启动能力，不能再内部创建默认物理 adapter；已拥有的冷义务仍可通过注入的恢复能力查询和停止。ToolRuntimeScope 只在已知 durable workspace 时创建生产端口，或使用调用方显式提供的端口。Manager 的非终端 runtime 明确传 None，受控端口保持自己的归属；不把“改成必需参数”误作绕过原 durable gate 的许可。启动、冷接管和 orphan settlement 的组合入口已全部迁移。

## 问题归属

| 编号 | 类别 | 处理 |
|---|---|---|
| PA-01 | 原拆分后遗留的契约缺口 | 请求与完整物理 runtime 同工程，PTY 词汇含完成源，业务入口可直接取得默认 runner；现分编译边界、迁实际调用者并删旧入口。 |
| PA-02 | 本轮分离引入、发布前修正 | GitGateway 此前借大工程间接取得 Deadline。主仓聚合构建可过，独立真实消费者编译失败；已补它实际需要的 Deadline 契约引用。 |
| PA-03 | 本轮接口迁移的中间编译失败 | 两处冷恢复调用和旧 optional-port 判断未迁完；Fable 拦下后已统一。最终显式端口缺失仍先拒绝，不以默认 adapter 补齐。未把这些失败版本提交或部署。 |
| PA-04 | 既有修复的验证登记遗漏 | PtyScope/PtyRecovery 的原生物理计时此前未登记进 raw-time 精确文件名单；源码及扫描器与修前相同即可复现四项命中。按 time-capability-004 登记两个实际物理 adapter，未豁免 Process 目录或业务代码。 |
| PA-05 | 既有 Bun 测试前提问题 | PATH 例在产品调用前死于 Bun 自身 spawnSync 的相对 PATH 搜索；改用同 cwd/env 的真实 POSIX exec 作参照。TOML 例死于 Bun 未支持 Node 子测试；九个原文反例改为同文件九个平级测试，所有断言保留。Node 计数因此少一个空父包装，不能说少了一项业务证明。 |
| PA-06 | 本轮 review 误判，已撤回 | 包装器末条 verdict 后未完成，一度怀疑句柄泄漏。原生完整 025 为 124/124、003 为 60/60，均正常退出；根因是接下来的真实有界等待约 5.3 秒，超过包装器 5 秒静默窗。修前 2e0f 包在相同 025、相同位置同样被终止；没有确认本轮句柄泄漏，也没有为“修泄漏”改生产。 |

## 验收证据

日志均在 `/private/tmp`；重复执行、skip、TODO 不重复算作业务通过。

| 范围 | 最终证据 |
|---|---|
| 原 012 | `process-a-20261018-contract-red.log` 修前失败；分离后 inventory 通过。 |
| 真正独立 Fable | `process-a-20261018-boundary-final.log` 2/2；纯契约正例与五类负例、实际 GitGateway 注入后执行一次并保留实参/退出码、旧默认入口编译拒绝、实际 PTY 消费契约正例与物理 adapter 负例。 |
| Node 全部相关 128 文件 | `process-a-20261018-native-related.log`：994 项，897 pass / 0普通fail / 25 skip / 72 TODO，进程 exit0。包括完整 Process/managed lifecycle/Delegation/Change/Git convergence/crash recovery/time，以及 durable-events-022。完整预算 256/549 仍作为 B 的已知失败 TODO，不藏成通过。 |
| Bun run/输出/契约九文件 | `process-a-20261018-bun-run-fixed.log`：72 pass / 0 fail / 1 integration skip / 2 TODO。 |
| 真实 Host | `process-a-20261018-real-host.log` 1/1，实际网络请求、物理原件和新 OS 冷读；不代替后续完整 run provider 专项。 |
| 精确主包 | `process-a-20261018-package.log`：generation615，2076 members / 13,375,869 bytes，SHA-256 `2e500c8e27a27aeb96c75c60e1c5f07429e2c8de4f3cdae01a293b58d36278ed`，真实 pack、隔离默认入口通过。 |
| 同一安装包的实际消费 | `process-a-20261018-isolated-package-final.log`：Node/Bun 各执行实际 run/原文 wire，Node 两文件 23/23；完整 PTY 准入与重启矩阵 124/124，其中实际 Bun workers 也只加载安装包。测试 oracle 单独复制，测试直接导入的 wanxiangli 指向包内同一副本；安装 2076 文件和包摘要前后不变，临时目录已清理。 |

首次包外试验缺 TOML 测试 oracle，第二次缺测试本身直接导入的 wanxiangli 解析边；均在测试装配/导入处失败，未更改产品依赖来凑通过。Fable 外部探针先后暴露 Deadline 缺边、探针 emitted 路径和声明 npm 依赖装配、Fable 布尔字符串大小写；全部原日志保留，只有前者是本轮工程迁移问题。format/check/diff 门禁通过，主构建只用 `node scripts/build.mjs`。

## 仍未通过的正式监督入口

默认正式包装器仍有既有缺口，单列 CUT-00-R1：`process-a-20261018-main-regression.log` 在 025 的有界等待被终止；去掉 025 的诊断组又在 003 同类等待被终止，`process-a-20261018-related-final-node.log`。它们均无权威总计，不能拿已输出的 verdict 拼成全绿。修前对照 `process-a-20261018-before-025-wrapper.log` 同样失败。完整原生文件及 128 文件组实际通过，也不能写成“默认正式包装器已通过”。

未加大任何静默窗口或用例超时，未删测试、刷心跳或跳过真实等待。后续须为确切有界等待设计不可续期的监督契约，同时保留真实挂起反例；与暂缓的 R12-U1 原自然 Unknown 分开处理。本包进入 REVIEW，尚待非作者复审；不关闭整个 CUT-02、GAP-092 或 PTY 脱组/Bootstrap 缺口。正常提交推送 master，不部署用户 Host。
