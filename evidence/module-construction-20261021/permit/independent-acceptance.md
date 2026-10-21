# MF05 permit 独立发行包验收

候选 `/private/tmp/wxl-permit-exact-20261021`，HEAD `9c73270674a743b24eb5c522b8125986a94e0198`，另含 root 的 3 个未提交 metadata/inventory 文件。验收未修改生产源码、metadata、主仓、构建入口，未提交或推送。

## 结论

本轮未发现阻断公开 permit 包接入的问题。21 模块构建、真实 tarball 安装、Node22/Bun 公开消费、严格 TS 正反用例、15 条领域规则及旧 20 产物连续性均通过。该结论限于独立库发行边界，不代替主仓 Host/provider/容量账本接线验收。

| 项目 | 实际结果 | 证据 |
| --- | --- | --- |
| 锁定依赖 | 实际 `npm ci`，9 packages，exit 0 | `library-install-network.log` |
| 全模块构建 | `node scripts/build.mjs`，21 个 `[build]`，exit 0 | `library-all-build-independent.log` |
| 源码对应产物规则 | Node22 15 pass / 0 fail / 0 skip / 0 TODO | `permit-node22-rules.log` |
| 源码对应产物规则 | Bun 15 pass / 0 fail | `permit-bun-rules.log` |
| 真实 tarball | `npm pack --ignore-scripts`，0.10.0，52 成员；已先构建全 21 模块 | `package-pack.json` |
| 干净安装 | 实际 npm install tarball，非 link，无运行依赖或编译器 | `package-install.log` |
| 安装包规则 | Node22 15 pass / 0 fail / 0 skip / 0 TODO | `permit-installed-node22-rules.log` |
| 安装包规则 | Bun 15 pass / 0 fail | `permit-installed-bun-rules.log` |
| 独立公开消费 | Node22/Bun 各 21 入口 import + 6 组行为通过 | `package-node22.log`、`package-bun.log` |
| strict TS 正例 | exact bigint/number、公开 DTO、判别联合、全部 permit 方法，exit 0 | `consumer-positive.ts`、`typescript-positive.log` |
| strict TS 负例 | 8 个错误均被真实拒绝，TS7 exit 1，无模块缺失错误 | `consumer-negative.ts`、`typescript-negative.log` |
| 旧模块连续性 | 相对 c4，20 exports 与声明字节一致；20 JS 仅构建 PID 注释不同 | `artifact-continuity.json` |

Runtime：Node v22.13.0，Bun 1.3.14。消费者在 `/private/tmp/vibe-fs-mf05-permit-audit/consumer`；执行 cwd 为 `/private/tmp`。清除了 NODE_PATH/NODE_OPTIONS。包内无 `.fs/.fsi/.fsproj`、build scripts、evidence、tarball，消费者无 `@fable-org`、TypeScript、esbuild。实际导入均解析到消费者的 `node_modules/wanxiangli`。strict TS 编译工具来自候选 devDependencies，未安装进消费者；随后运行包不依赖编译器。

安装包的 15 规则保留原测试正文，只在临时消费者复制件中把 import 从相对 dist 改成公开 `wanxiangli/permit`。生产测试文件未改。

## 具体边界

- 相邻 `9007199254740992n`、`9007199254740993n` credit 签发不同许可，精度和 bigint fence 保持。
- 7/7n 双向重入在 Pending、Committed、两种 Released 情况均返回同一原始冻结对象及原编码，不重新激活终态。
- fence 同值跨 number/bigint 编码仍拒绝；错误值、字符串、null、undefined、NaN 对查询和两条显式 mutation 都不改变状态。
- 同形伪造、跨实例、被替换旧 handle、旧 physical completion 均不能改变后继许可。
- `Int64.MaxValue` credit 接受；非法或溢出 credit、错误 identity 拒绝后，原 permit 不变，下一次合法许可 fence 仍为 2。credit 溢出的“拒绝前无 mutation”有真实行为证明。
- caller 修改原 identity/target 不会改变签发时复制并冻结的身份，后续错误 observed 身份为 Conflict。
- 旧 20 模块基线是主仓库 HEAD `c4bc2caf00d3ef8229e95efacb9f73fe130657f9`，工作树干净。产物比较只归一 `// .build/<module>-<PID>/` 的 PID；没有删除其他代码、空白或字符串。20/20 归一后逐字相同；未经归一的 20 文件均因 PID 注释不同而 SHA 不同。不得称其原字节相同。

## 有限验收范围

没有执行 2^53 或 Int64.MaxValue 次签发；计数器耗尽的两个拒绝点仍仅为源码与真实编译产物的静态顺序证明：`ExecutionPermits.fs:228-237` 检查上限，`issue:295` 先求 next，之后 `305-307` 才改 counter/issued/session。没有引入 counter seed、测试后门或生产 hook。credit 最大值通过不能替代 counter 耗尽的动态证明。

没有重跑其余 20 模块所有行为套件；本轮依据原 exports、声明和构建代码连续性，以及 Node/Bun 的公开入口实际加载。没有证明主仓准入、SDK callback、物理 completion、配额 owner 的接线；没有新增持久化或冷恢复保证。

## 保留的失败证据

- `library-all-build.log`：root 首次缺 esbuild，构建尚未开始，保留原样。
- `library-install-independent.log`：首次 npm ci 被沙箱 DNS ENOTFOUND 阻塞，终止后才使用联网权限重试；未覆盖失败。
- `package-audit-cache-rejected.log` / `package-pack-cache-rejected.json`：初始 pack 默认 cache 不可写；改为本证据目录独立 cache，无改用户 cache 权限。
- `package-audit-ts-exit-expectation.log`：验收脚本最初按旧版 TS 预期负例 exit 2；实际 TS7 以 exit 1 拒绝且输出同 8 个类型错误。只修正临时验收脚本预期，生产协议未改。

## 指纹

- tarball SHA256：`bf27ee68df4d7cb2fbd70b0b4b80f2085f5b6c270a27a9d6c2682a374e75046e`
- package.json：`54f6baa5591b910a310eccc6cf43fd4a00b55dee5cc5aa7eace50403ff22af8b`
- package-lock.json：`4e1457246718853aa11340e14f0500d2442c2197bd6cfc0027fd4db9146927e9`
- scripts/tests/exports-package.test.mjs：`18c8312b5f5e6352beceb71a44f372af6ce02194819e1f5993dcaecec953c5b2`

结束时候选 git status 仍只有 root 原有三文件；`git diff --check` exit 0。所有本轮构建/测试/安装命令已返回，未启动后台消费者。完整包验收入口为同目录 `package-audit.mjs`，不修改任何库输入。
