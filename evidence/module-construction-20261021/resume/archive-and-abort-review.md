# aad92fa0b pause 存档独立只读复核

主仓 `/Users/yuanxi/.codex/worktrees/upstream-plan-20261020/vibe-fs`，读取 HEAD `aad92fa0b476e5a8777c505a64656fec0d1fd2d0`，工作树干净。未改源码、检查器、index 或原始证据，未跑全量或任何 native probe；只运行只读 inventory/freshness/package closure 函数。

## 查实的问题

**P1（下一次正式验证前须解决）：归档改变生成语料输入，旧 build 失鲜。**

`scripts/lib/loop-detector-repository-corpus.mjs:10-29,54-63` 从 Git 已跟踪文件收集 `.mjs`、`.md` 等源码/文档，既不执行文件，也没有排除 evidence。pause 下 15 个源码/文档实际进入 `collectGeneratedInputs`。package.json/lock 的 JSON 不属于这份语料扩展集合。

读时 gen106 的真实 `assertBuildFresh()` 结果为：

```text
generated-input-stale
expected 3175cc7fab67a640efe55c08865c96fcd9643ed9578ae4d0d9b36c5f97fce184
current  29320060f7fe2005227135d9ec28c2c74d4766968fcfd6d6d084da20dddd5cbb
```

依据 `scripts/lib/build-state.mjs:417-425`，这会阻止正式测试入口把旧产物当成当前输入的通过证明。变化不限 pause，当前提交的其他已跟踪报告也可能共同贡献 digest；这里实际枚举确认了 pause 的 15 项成员，未把全部 digest 差异都归咎于它。

**最窄处理：**依既有 `node scripts/build.mjs` 正常重建生成候选，再按正式入口验证相应输入。保留原始存档和原生成语料规则；不通过重命名原证据、修改已记 digest、增加 evidence 特判或放宽 freshness 来取得绿色。这些文件属于 loop-detector 产物的生成输入，是否改变具体输出由正常重建核验判定，不能称为“只加证据，因此无需重建”。

## 没有查实为问题的边界

| 对象 | 实际依据与结果 |
| --- | --- |
| 存档 manifest 的 `file:...wanxiangli-0.9.0.tgz` | `scripts/wanxiangli-source.mjs:23-31` 只按 Git **文件名**检查实际 tracked `.tgz`；`:54-62` 只检查项目根 manifest 的 wanxiangli source 依赖。没有递归安装/解析存档 manifest。实际 `checkSourceDependency()` 通过。 |
| npm 安装与 CI | 根 manifest 无 workspaces 指向 evidence；`.github/workflows/ci.yml:25,42` 只在根运行 npm ci/verify:release。归档 manifest 不会改变根锁文件安装。 |
| 自动测试发现 | `requirements/verification-system/tests/run.mjs:78-88` 仅发现 requirements 下 `.test.mjs`，归档 harness 均为普通 `.mjs` 且在 evidence 下。静态 semantic-test 扫描同样以 requirements 为根。没有主仓 src/scripts/requirements/resources 对 pause harness 的执行 import。显式手工 TESTS_MJS_FILES 当然能指定任意文件，但不是自动发现。 |
| 包分发 | 根 package.json:14-17 的 files 只有 dist/resources；bundled wanxiangli 也由自己的公开文件清单派生。实际 `deriveExpectedClosure()` 得到 1380 成员，evidenceMembers=[]。本轮未重打包，因此这是实际闭包派生与发布白名单证明，不是新 tarball install 证明。 |
| 归档相对路径 | 放平后的 archive/package.json 相对 tgz 路径不能从存档目录直接安装；pause/README 与 PAUSE-HANDOVER 已要求在新的临时 consumer 重建原布局。这是历史凭证，不是活跃依赖。没有理由为通过现有门禁改原 bytes。 |

## 凭据检查

`pause/pty/recovery/request` 确实保存了一个 **64 字符随机测试控制 secret**，此处不复制值。它不是普通 nonce：`PtyRecovery.fs:428-435` 使用 key/incarnation/secret 验证 attach；`:793` 由每个新 recovery owner 随机生成；原终态 receipt 的 HMAC 也使用它。因此不能笼统说“归档完全没有任何凭据”。

但这份 secret 的权限只属于已经结束的该 PTY 测试实例：归档的清理 receipt 显示 owner/目标关闭，且本轮只读 `existsSync(request.endpoint)` 实际为 false。没有发现现用 provider API key、Git/npm auth token、私钥或真实外部 Host 登录凭据；高置信度常见凭据标记搜索没有命中。该搜索不是对所有未知编码的密码学保证。

**有限结论：**存在已失效测试实例的历史控制凭据，未查实仍可用的运行凭据泄露，不是当前产品阻断。保留原证据。复跑必须按交接用新目录、新 key/incarnation/secret，不能拿归档 request 恢复控制；不应把已经公开的历史 secret 当作今后的认证权威。公开 secret 后重新计算 HMAC 只能说明归档数据内部一致，不能单独证明其原始来源；既有 Git 历史和采集时 receipt 仍须保留。

## MF02A：exact-attempt abort 的实际缺口

已修好的本仓部分仍成立：

- `ToolRegistry.fs:1135-1161` 仅由 owning assistant→真实 user parent 推导 delivery physical；没有 ambient current fallback。
- `ToolRuntimeScope.fs:1195-1203` 的 question 与 `:1256-1280` 的 post-S3，在派发前核对精确 physical，能跳过 **在本地检查前已准入** 的 B。
- 但 `SessionHostPort.fsi:14-15` 的 InterruptAttempt/AbortSession 都只有 sessionId；`Sessions.fs:384-399` 继续调用 `port.AbortSession sessionId`。
- SDK adapter `OpenCodePort.fs:207-217` payload 只有 path.id；HTTP adapter `:428-432` 为 `/session/{id}/abort` 空 body。当前 SDK `dist/v2/gen/types.gen.d.ts:8506-8530` 同样 body?:never，无 expected physical/run/epoch，响应只是 boolean。

因此“本地检查时 A 仍 current”与“外部 Host 实际执行取消时仍只针对 A”之间没有端到端原子保证。question 返回后的再检查只能诊断，不能撤回已发生的 abort；post-S3 settled 集合也不能约束远端取消对象。

存档真实 gen102 编译 plugin 反例是：A 通过本地 guard，SDK transport 等待；B 经真实 chat.message durable Accepted；释放 transport 后旧 payload 仍只有 sessionId。最终 safety assertion 真实失败。B 的 providerStarted=false，**这不是实际 B provider 被杀的证明**；只是现有公开协议不能表达“绝不取消 B”的精确条件。

### 最小外部协议

需要 Host 提供一种实际能力，二选一即可，不需要新通用框架：

1. Host 为目标物理执行签发不可重定向的 cancellation capability；或
2. `cancelExact(session, expectedPhysical, expectedIncarnation[, expectedProviderRun], requestId)`，Host 在实际副作用前原子核验并捕获只属于该执行的 cancel handle。

目标必须先定义清楚：本链意图是停止工具所属 physical execution 的输出，不能只捕获一个会继续处理下一物理输入的 session runner fiber。若按 provider attempt 取消，则其 run/epoch 也须绑定本次意图；若按 physical execution 取消，同一 physical 的合法后续 step 可以受控，但新 physical 永远不应继承这个 cancel handle。

必要语义：

- mismatch/stale/unknown/unsupported 不能发 session-wide abort、取消无关 background jobs、清理新输入或新 provider；校验和捕获之前不能先产生这些副作用。
- 一旦捕获 A，取消在途期间即使 B 准入，也只能触达 A；给 session.abort 多添一个被忽略字段不算支持。
- 重复 requestId 对同一 target 幂等；transport unknown 不自动重发，观察接口/receipt 仍绑定原 target，不重新解析 session current。
- 区分“确定未尝试/过期/不支持”“已请求”和“该 target 已物理结束”；HTTP 200、boolean 或 Requested 不等于停止收尾完成。后者须有 exact target 的完成/资源证据，才可驱动对应本仓结算。
- 原 root/user 中断权限、question 不取消人类回复/不 drain children、retirement 的 own/drain 合同保持；exact attempt interrupt 不能偷换成整 session AbortChildren。

### 仅靠当前接口可以做什么

可以继续保持/加固 owning physical 绑定、missing-proof 不派发、重复事件幂等、旧注册清理、独立 read-only replay 和诊断；也可以把 expected identity 显式带到本仓 port 边界，并对不支持的外部能力明确拒绝。后者若不发送任何旧 session abort，只证明 fail-closed，不证明原中断功能仍可用，不能伪称完整修复。

不能通过额外本地 current 检查、JS mutex、在途结果后二次检查、轮询快照，或阻塞本地 chat.message 来获得现有 Host 不提供的取消原子性；外部 Host 的输入和 runner 不受本地锁约束。当前 API 无法同时证明 exact safety 和原自动中断功能可用。这个残余需要 Host 合同，不能在主仓 adapter 里猜一个 fallback。

后续接收应先有当前 A 的正控停止和 exact 物理回执，再在 Host 真实取消副作用边界挂起 A 请求、令 B **实际 ProviderStarted/运行**，释放旧请求并证明 B 不中断；覆盖同 session 新 physical、同 physical 新 provider step、重复和 Unknown。当前受控 SDK Accepted-B 反例不能代替这些 Host 证明。本轮未启动这些新实验。
