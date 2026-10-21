# MF01 isolated B：独立验收与生产接收边界

结论：B 可接收为 **有限的 guest Callable RPC 机制候选**。独立原16项及新增7项在 Node22、Bun 都通过；same-realm thunk 参数、exact proof receiver、object veto、finish 单次消费及 Closing 终局均有实际调用证据。**不能直接接到生产命令解释器**：通用 `passive()` 会拒绝旧版合法的 class command，或调用旧版原本不会调用的 toJSON 改写命令。这一接生产前 P1 已由旧 compiled Workflow 真正执行的正控与 B 两个 runtime 的反例共同确认。

未修改任何候选源码、主仓源码、index、公开库、元数据或构建产物。本轮不构建、不提交、不推送、不启动外部 Host。敏感运行预约已交还 root，无仍运行实验。

## 输入与环境

候选 `/private/tmp/vibe-fs-mf01-vertical-a-callable-20261021`，九输入固定于 `/private/tmp/vibe-fs-mf01-callable-pre-run.sha256`。

- owner `aafe9127b23eac82cbaf57865dd0caeeafb3f99abbb3c69fee9b2f386dad3989`
- guest `026bf0135f2270ef745513583803a4e3a42ef5268bb61b860a703a745c438491`
- callable-realm `6479295d54a620ce4e1c56b0c26b4ca27df1815bf6922639485658aec70f5f15`
- callable.test `50111bbab9b6c0901536c42c1db1913448ccc59774e75c6c391a124fe9f1fb6b`

其余五份见原 manifest。独立开始前 `/private/tmp/vibe-fs-mf01-callable-independent-before.log`、结束后 `/private/tmp/vibe-fs-mf01-callable-independent-after.log` 的单独 hash 校验命令各 exit0，九项全部 OK。

Node22.13.0 路径 `/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node`；Bun1.3.14 `/opt/homebrew/bin/bun`；macOS26.6.2 / Darwin25.6.0 arm64。运行全部串行：Node base16 → Bun base16 → Node extra7 → Bun extra7 → Node 旧 compiled 正控2 → Node B兼容反例2 → Bun B兼容反例2。

## 实际结果与独立退出

每条测试命令独立取得退出值；没有把后续 hash 命令的退出当作测试退出。

| 集合 | Node22 | Bun | 原日志 |
| --- | --- | --- | --- |
| 作者原16项 | 16 pass / 0 fail，exit0 | 16 pass / 0 fail，exit0 | `/private/tmp/vibe-fs-mf01-callable-independent-base-{node,bun}.log` |
| 独立新增7项 | 7 pass / 0 fail，exit0 | 7 pass / 0 fail，exit0 | `/private/tmp/vibe-fs-mf01-callable-independent-extra-{node,bun}.log` |
| 旧生产 compiled 正控2项 | 2 pass / 0 fail，exit0 | 未跑 | `/private/tmp/vibe-fs-mf01-callable-command-old-control-node.log` |
| B生产接收兼容反例2项 | 0 pass / 2 fail，exit1 | 0 pass / 2 fail，exit1 | `/private/tmp/vibe-fs-mf01-callable-command-compatibility-{node,bun}.log` |

Node 各集合 cancelled/skip/TODO 均0。没有测试自身 fixture 错误或环境失败；兼容红是有意按旧生产合同断言得出的真实差异，未放宽为通过。旧A10仅作者在此B目录跑过，本轮没有将它写成独立复跑事实。

## 七项实际独立证明

1. sanity 的参数数组保留原 guest 对象同一引用、function闭包、undefined、稀疏位、-0和数组额外的function属性；没有穿过 JSON/passive wire。thunk 以 undefined receiver 调用，next/watch 用同一 proof 实例及其 mutable receiver，proof 从41实际写盘到42。新 execution 是另一个构造实例，calls 为0；同一个外部lexical对象仍保留预期共享引用。release 后 proof 不可再次调用。
2. watch veto 的 own abort 为 false、0、空字符串也均被接受；null/undefined不接受，继承的 abort不接受。own abort getter只读一次，执行于guest。true及undefined verdict不冒充veto。
3. constructor、metadata getter、method getter、thenable assimilation、toJSON 全由真实guest代码依次通过实际sync Host RPC写盘，顺序 `CSGGTJ`，精确6次write；未在Host调用原对象getter或函数。该正控仅证明代码归属与当前投影行为，不能推出通用toJSON适合生产命令。
4. declared `JsFailure`、普通Exception、循环passive结果、结果getter异常分别保留本候选声明的code/family，未关闭仍活跃offer。没有据此宣称所有生产JsFailure字段已迁移。
5. 丢失finish确认后，原业务一次写盘并进入第二offer；旧、clone及跨run真实ref均不能消费新offer；Host拒绝的无效ref不推进commandSequence。旧proof在guest已退休，不能读spec。fresh owner同本地id不受旧ref影响。原丢回执调用最后以不可确认错误结束，不重新执行finish。
6. execution的onExit回调真实park，在ps观察T后签发site；clone及已消费site不能resume。同一instance从41写盘到42，回执保持原context及注入值，再完成exact offer。
7. B安装Callable注册端口后，真实输出chunk的Closing竞争仍正确：转交真实result前调用同owner.kill()，对外只发布Closed。不是fake dispatch，未伪造frame/runId。

原16项还提供 constructor/getter/thunk/abort getter/after-await/thenable/toJSON loops 的外部watchdog/实际SIGKILL、caught abandon真实finally write/rm与拒绝再park、loopingfinally不冒充programSettled 的有限证据。本轮原样独立运行这些用例，不将它们升级为生产ledger或全部deadline合同。

## P1：generic passive 不能作为生产next/watch命令投影

根因：`callable-realm.mjs:140–142` 对所有callMethod结果统一调用 `passive()`。该函数在60–61行主动获取并调用toJSON，64行拒绝非plain prototype。

现主仓 `src/Wanxiangshu/Process/JsCallLoop.fs:758–779` 的实际parseStepCommand只统计own spawn/signal/send/waitFor，随后按对应字段解释；`parseSpawn` 在702–705行读取name/spawn字符串，不要求plain prototype，也不调用toJSON。本轮正控两字段均为own字段。旧membrane把合法对象交到这个解释边界，不能以新的“只接受JSON形状”缩小旧能力。

### 旧 compiled 真实正控

直接调用当前主仓的 `dist/Repository/Programming/Js/WorkflowSurface.js` execute，沿与requirement050相同真实入口运行this.call。两项分别返回：

- `new Command()`，其own name='probe'、spawn='printf OLD_COMMAND > command.marker'。
- 普通对象同样有own name/spawn，另有toJSON函数会返回 `{different:1}` 并递增局部计数。

两项均实际生成scratch command.marker，内容OLD_COMMAND；实际Workflow返回Succeeded/completed、scopeClosed=true、endUnconfirmed=false、outputComplete=true，toJSONCalls=0。证明是原生产解释链可用行为，不只是根据源码推断或镜像一个parser。执行的shell只属于该测试自己的scratch scope，未连接其他Host；原生资源收尾在这里依据实际Workflow的scopeClosed回执，本文件未额外捕获每个shell PID，不能称其为独立PID清单核验。

实际加载的关键compiled输入固定：

- WorkflowSurface SHA256 `423d80761d14d23c8834984b3094ea1edf8a615feb45a35e7d5492e8a20dbaa1`
- JsCallLoop SHA256 `7c933e7520da50513b84f2bc7046f08250b6bdd98369dc8b4345118ef2c93f0e`
- JsSandbox SHA256 `801f782a224500256863f94f0d1a698b37d41df54ace7af3cfbea4013a465f2b`

清单 `/private/tmp/vibe-fs-mf01-command-old-control-inputs.sha256`，结束复核 `/private/tmp/vibe-fs-mf01-command-old-control-inputs-after.log` 单独exit0。没有重新编译主仓；这三份是入口关键输入指纹，不声称记录了整个运行依赖闭包。

### B两个runtime真实反例

同样合法的callback结果经B的真实guest端口：class Command被拒为 `CALL_RESULT_INVALID/JsFailure: Result is not a plain object`；普通对象则变成 `{different:1}`，并且toJSON中的真实scratch write多执行了一次。两个runtime结果完全一致。B兼容测试不执行shell，只验证真实callback投影与副作用归属。

这不否定B的隔离RPC机制，却阻断直接接生产。最窄下一包应在guest按调用用途解释原raw结果：next投影原StepCommand合同，watch投影原verdict合同，onExit按原语义处理其返回值；只有相应schema的被动DTO跨进程。读取own字段、getter与必要转换仍在guest原owner中进行，不重建函数、不冻结原对象、不调用无关toJSON。不能用允许任意prototype之后继续调用toJSON的通用放宽替代此边界；也不能为过测试修改旧生产parser。

## 收尾与证据源

所有B新增用例每个owner都等native Closed/failure null，检查PID ESRCH、accepted0、callablePending0、FIFO不存在、旧FD fstat/EBADF后才删除私有temp目录。

机制extra7 Node实际PID：51173、51176、51179、51182、51185、51188、51191、51197；Bun：51238、51242、51246、51250、51254、51258、51262、51269。各原日志末尾有all-owned-absent及路径。

兼容反例Node PID51679、51682；Bun51753、51757。均已实际SIGKILL/Closed、failure null、ESRCH；没有活跃保留owner、未回收FD/FIFO或自建temp目录。

本轮三个独立源与指纹：

- `/private/tmp/vibe-fs-mf01-callable-independent.test.mjs` SHA256 `d039eb4e21df27d9f414ad9fdb99f29a07fcbab7fe569d3ad39311454a4451a4`
- `/private/tmp/vibe-fs-mf01-callable-command-old-control.test.mjs` SHA256 `a22602dc75cea46e19d899199967d666d3e07a93a5fbe8644b57dcd89e0f2d2e`
- `/private/tmp/vibe-fs-mf01-callable-command-compatibility.test.mjs` SHA256 `185cbea5e876ad1b03f82b11ba542770052979a1052d2cb59ecb52fc1028c506`

清单 `/private/tmp/vibe-fs-mf01-callable-independent-harnesses.sha256`。无新npm依赖；机制及B兼容测试依赖固定九输入，旧正控另依赖上述主仓当前dist及其正常依赖。

```sh
cd /private/tmp/vibe-fs-mf01-vertical-a-callable-20261021
shasum -a 256 -c /private/tmp/vibe-fs-mf01-callable-pre-run.sha256
/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node --test callable.test.mjs
/opt/homebrew/bin/bun test callable.test.mjs
/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node --test /private/tmp/vibe-fs-mf01-callable-independent.test.mjs
/opt/homebrew/bin/bun test /private/tmp/vibe-fs-mf01-callable-independent.test.mjs
/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node --test /private/tmp/vibe-fs-mf01-callable-command-old-control.test.mjs
/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node --test /private/tmp/vibe-fs-mf01-callable-command-compatibility.test.mjs
/opt/homebrew/bin/bun test /private/tmp/vibe-fs-mf01-callable-command-compatibility.test.mjs
shasum -a 256 -c /private/tmp/vibe-fs-mf01-callable-pre-run.sha256
```

最后两条兼容测试在此冻结点应exit1，不能按“全部命令退出0”解释此报告。

## 未验与未接线

生产StepCommand/verdict/onExit投影、完整sanity诊断及park/fail策略仍需后续包；borrow/reset与固定guest60000ms上界不在本次认领范围，没有把未实现借用当作新产品失败。production shell owner、真实Host authority、ReadOutput、Prepared/Committed ledger、跨scope/resource许可、实际内存硬限额与极端OS cleanup均未由scratch证据关闭。此报告也不证明所有结果后未await continuation路径；A已保留该前提未成立的独立记录。
