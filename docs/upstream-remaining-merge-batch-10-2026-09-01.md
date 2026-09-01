# Upstream Remaining Merge — Batch 10

## 范围与基线

第 10 次只实施 M10 property pilot。upstream PR #27 尚未合并，因此按负责人指示从第 9 次最终节点 `0bab43b17` 累计开发；分支为 `codex/remaining-merge-batch-10-fast-check`，施工时最新 `upstream/master` 为 `d76a4a8b5`。

本批不回放重构前的 property 套件，只选择两个状态空间确实需要生成与 shrinking 的现行 WHAT：

1. `MANAGED-SESSION-007`：同一 durable handle 的多个完成事实竞态中，production 必须只接受首个事实。
2. `PREFIX-STABILITY-001`：同一 provider epoch 只允许 wire history append；任何既有 role 或 part 字段变化都必须形成 cold boundary。

没有修改 production F#、公开运行时 API、baseline、suppression、allowlist 或门禁阈值。

## 依赖与执行政策

- 根 `devDependencies` 直接、精确固定 `fast-check@4.9.0`；不依赖传递版本或浮动 range。
- production invariant 每条固定 seed、`numRuns: 1000`；定向 mutant 每条固定 seed、`numRuns: 100`。
- 失败保留 fast-check 原生 `seed + counterexamplePath`；mutation proof 必须用该二元组实际重放已缩小反例，不能只检查 path 非空。
- generator 只生产输入；oracle 必须是 WHAT 中可独立陈述的关系或 typed rejection，并直接调用注册 production Surface。
- 小而封闭的有限域继续穷举；静态 ownership、真实 Host/network/process 或没有独立 oracle 的问题不使用 property testing。
- shrinking 暴露的历史缺陷应固化成普通最小反例；本试点不引入全仓 mutation framework。

## Git 节点与因果

1. `6e4126b4c build(test): pin fast-check property runner`
   - 增加 exact direct dependency 与 lockfile 根声明。
   - 离线 lockfile 更新成功；`npm ls fast-check --depth=0` 解析为 4.9.0，audit 为 0 vulnerability。
2. `8391e8e6a test(handle): property-check production completion races`
   - 删除 `join-completion-property.test.mjs` 中 251 行测试自建 lifecycle/deadline/forced-completion 状态机。
   - 生成 agent、PTY、manager-job 三类 handle、2—64 个 Terminal/SendFailure/Cancelled arrival；全部通过注册 `Execution/Delegation/Handle/Surface` 执行。
   - 同时断言 first-wins、所有 late completion 精确返回 `AlreadyCompleted`、输入 projection 不变、相邻 decoy handle 不变。
3. `9ad1d1df6 test(prefix): property-check production wire stability`
   - 生成 metadata、history、extension 与 production 支持的五类 WirePart：text、reasoning、tool-call、tool-result、media。
   - 正性质证明 append-only；反性质逐字段修改历史 role、text、reasoning、tool call/result、media type/digest，要求 production 拒绝。
4. `d23929a90 test(property): replay minimized mutation failures`
   - handle 的 accept-late/last-wins mutant 与 prefix 的 length-only mutant 均稳定被杀死。
   - 两个 proof 都使用 fast-check 返回的 exact seed/path 再跑一次，证明缩小结果可重放。
5. `bd2b67f4a spec(verification): constrain production-bound properties`
   - 将上述适用边界、预算、seed/path、production Surface、禁止镜像 oracle 与 mutant 规则写入 verification-system WHY/WHAT/HOW。
   - managed-session 与 prefix HOW 精确链接现存 executable proof。

## 发现并消除的既有问题

原 `join-completion-property.test.mjs` 名为 property，却完全在 JavaScript 测试内重建 handle 状态机，还包含 deadline、forced completion 与 wall-clock 风格状态。它可以在 production owner 错误或完全未被调用时继续全绿；同时与当前“状态只能被事实改变”的时间无关语义冲突。

本批没有为该镜像补更多 case，而是删除平行模型，让每个生成样本穿过 production Surface。测试只保留独立的 first-wins 关系、typed late rejection 与非目标对象不变性。于是“改测试内状态机/注释让门禁变绿”的捷径失去作用；若 production 接受第二次完成，固定 mutant 所表达的错误世界会立即幸存并使 proof 变红。

prefix 侧没有复制 production equality/canonicalization 公式。generator 只构造完整合法 wire 与精确单字段历史 mutation；oracle 是 `oldHistory ⊑ newHistory` 这一 WHAT 关系。length-only mutant 证明仅比较消息数量会被稳定识别。

## 验证与成本

- 两个 property 文件：9/9；重复运行约 0.37—0.62s。
- managed-session package：125/125；约 1.61s。
- prefix、trace、surface focused：111/111。
- requirement trace / Surface Manifest charter：38/38。
- requirement trace 全树：772 WHAT / 3904 tests，closure complete。
- Surface Manifest：165 registered surfaces closed。
- `node scripts/build.mjs`：738 F# sources / 165 surfaces。
- `node scripts/check.mjs`：全部 text gates 通过。
- `node scripts/check.mjs --lane=owner-dep`：27,226 strict FCS uses / 624 owner edges / 778 contracts；authority、composition、DSL、decorator gates 全绿。
- `WIREIT_CACHE=none npm run format-build-test`：允许本机 loopback 的正式环境中完整通过；authoritative unit 3856/3856，全部 integration、package、273-case harness、Long Stroke 与 `npm pack --dry-run` 通过。

首次在受限 sandbox 跑完整阶梯时，唯一失败为真实 Host canary 无权监听 `127.0.0.1`，错误是 `listen EPERM`。同一 canary 在允许本机 loopback 后通过；没有为此修改 upstream 或 production，也没有跳过测试。

文档后验证第一次把 Surface charter 与仍在运行的 clean build 并发，charter 在 `dist/` 清空后、产物尚未写回时报告 missing surface；这是验证编排错误。等待 Fable 明确 `build ok` 后，原命令 38/38，manifest 165/165。该次无效 verdict 不计为产品失败，也没有据此修改代码。

新增 property 的 focused 增量小于一秒；完整阶梯的主要成本仍是既有 FCS 与 Host/integration 检查。固定 seed 保证普通 CI 稳定，shrinking replay 保留诊断价值。

## 能证明与不能证明的边界

本批提高的是两条明确 invariant 对大量输入组合和最小反例的敏感度，不声称 comprehensive correctness。fast-check 不能证明 oracle 正确，也不能发现 generator 域外的语义；因此它受 production Surface、独立 WHAT relation、定向 mutant 与 review 四重约束。

两个性质的 generator 域已显式覆盖当前公开构造；未来新增 handle kind、completion kind 或 WirePart case 时，应让类型/Surface consumer 与 property generator 同步变红。小型有限状态空间仍应穷举，不能为了统一工具而改成随机抽样。

## PR 边界

PR #27 尚未进入 upstream，故本批无法在 Git 图上成为独立于 M0—M9 的 PR；它将作为从 #27 最终节点继续的累计 PR。owner 只应合并最新累计 PR，再关闭旧 PR，避免重复应用提交链。PR URL 在创建后补入本节。
