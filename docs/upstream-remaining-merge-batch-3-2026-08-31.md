# Upstream 剩余语义合并：第 3 次

## 1. 范围与基线

- 执行分支：`codex/remaining-merge-batch-3`。
- 基线：`upstream/master@fcd5ab11b`。
- 模块：M5 ambient-time、M4 causal-wait、M6 Host boundary。
- 本批只加固 proof 与唯一 observation capability；不改变时间、等待或 Host signal 的产品规则。
- 历史提交只用于确认缺口。全部修改在当前重构 owner 上重新实现，没有 cherry-pick 旧 patch。

## 2. M5：ambient-time 全生产树 fail-closed proof

### 原 upstream 缺口

`TIME-004` 的 gate 只扫描预选目录。不存在的扫描根返回空结果；目录级排除会放过同目录新增文件；`Process/Runtime.fs` 还被 Surface Manifest 误列为该 law 的行为 proof。三个反例可在 production 引入 wall-clock correctness path 而保持门禁绿色。

### 修改

1. `16f9fca06 test(time): expose ambient-time gate escape hatches`
   - 先加入选定目录外、被排除目录内新增文件、missing-root 三类反例。
   - RED：旧 collector 仅 1/4 通过；三条逃逸路径稳定存活。
2. `b86a1a14b fix(time): make ambient-time proof cover production`
   - 扫描整个 `src/Wanxiangshu` production tree。
   - 例外由目录级改为 25 个 exact physical-adapter 文件。
   - missing root、非目录 root、root escape 全部抛出 typed collector error，禁止 empty-success。
   - `Process/Runtime.fs` 从 TIME-004 Surface law 中删除；该 law 只由 architecture proof 承担。
   - Structured Workflow 的 G4R gate 复用同一生产树边界与 exact-file discipline，避免保留第二种宽松扫描语义。
3. `8fabac0b3 docs(time): bind TIME-004 to the architecture proof`
   - HOW 精确链接 executable architecture proof，删除把行为 Surface 当成时间禁令证明的错误映射。

### 证明

- ambient-time focused suite：43/43。
- 正例保留合法 physical adapter；反例覆盖非 allowlisted production path、allowlisted directory 中的 decoy、missing root 与 path escape。
- 门禁没有增加 suppression、baseline 或目录级 allowlist。

## 3. M4：causal-wait 不可伪造 capability 与全树 gate

### 原 upstream 缺口

注册 Surface 用硬编码布尔值声称 observer 不能读 snapshot、reader 可以读；测试没有让类型能力决定失败。静态 gate 维护第二套局部 scanner，只覆盖有限目录与源码形状。删除 observation、挪到新文件、放进 multiline expression、comment/string decoy 均可能造成假绿或假红。

### 修改

1. `cc94401d2 test(causal-wait): expose fake capability proof`
   - 测试改为实际调用 `observerCapability` 与 `readerCapability`。
   - RED：20/23；旧 Surface 没有 capability operation，三个调用反例失败。
2. `a007e06f3 feat(causal-wait): separate observer and reader capabilities`
   - production Surface 暴露 opaque `ObserverHandle` 与 `SnapshotReaderHandle`。
   - observer 只能观测 completion；reader 才能读取 snapshot。
   - 两种 cross-use 返回 typed rejection；删除硬编码 capability 布尔值。
3. `19cc7e3f2 test(causal-wait): expose incomplete observation gate`
   - 将 gate 测试改为 shared-analyzer obligations；RED 1/8，证明旧 gate 无法满足完整 production 边界。
4. `34ac09e13 test(causal-wait): make observation boundary fail closed`
   - gate 扫描全部 696 个 production F# 文件。
   - 新增 `scripts/lib/fsharp-source.mjs`，统一屏蔽 comment/string/non-code 后再分析真实 token。
   - 六类 mutation fixture 覆盖 missing observation、wrong source、第二 observer、reader bypass、multiline 与 decoy。
   - collector 对 missing/escaping root fail closed。
5. `af24edd65 docs(causal-wait): document the single proof boundary`
   - HOW 删除硬编码 capability claim，改为生产 capability operation + 全树 architecture gate 的单一 proof boundary。

### 证明

- causal-wait focused suites：39/39。
- requirement trace：772 个 WHAT、3925 个 executable declarations 全闭合；完成 Host proof 后为 3926 declarations。
- production build：734 个 F# source、161 个 registered Surface。
- gate mutation 不重建 wait state machine；只验证 production owner、capability provenance 与禁止边界。

## 4. M6：Host snapshot locality 与 signal closed set

### 原 upstream 缺口

snapshot locality 只证明存在某个调用点，不能拒绝 missing 或 ambiguous owner。signal proof 只检查弱分类与部分字段；新增 case、丢字段或读取错误表示时可能保持绿色。

### 修改

1. `a9ad2a8da test(host): prove snapshot location fails closed`
   - 加入 exact production location、旁路 decoy、missing 与 ambiguous 四种世界。
   - 只有 Host identity owner 的唯一 snapshot consumption 合法。
2. `be98caaf5 test(host): bind the typed signal closed set`
   - 通过注册 `HostSignalSurface` 直接投影五个 typed signal case。
   - 对每个 case 断言 exact JSON shape；不存在额外 case 或未声明字段的宽松接受。
   - 断言对齐当前 upstream 的 `failure` / `diagnostic` vocabulary；没有恢复历史 `reason` 字段。

### 证明

- Host focused suites：13/13。
- 真实 OpenCode 1.18.18 admission canary：2/2。
- 普通 sandbox 唯一一次失败为 `listen EPERM 127.0.0.1`；在允许 loopback 的同一 canary 中通过。未修改、mock、skip 或放宽测试。

## 5. 对 upstream 原文件的修改

| 修改面 | upstream 原状 | 修改理由 | 反例 / 证明 |
|---|---|---|---|
| ambient-time proof 与 collector | 预选目录、目录排除、missing-root empty-success | TIME-004 必须覆盖完整 production correctness path | 三类旧 gate 假绿先 RED；43/43 GREEN |
| G4R collector | 自有较宽扫描与屏蔽实现 | 与全生产树、exact-file、fail-closed 根边界共用纪律 | 原 G4R vocabulary tests 保留并通过 |
| Wait Surface | 布尔值自述能力 | capability 必须由不可混用的 production handle 产生 | observer/read cross-use typed rejection；39/39 |
| causal-wait gate | 有限目录、重复 scanner、源码形状匹配 | 新文件与不同排版不能绕过 observation boundary | 696-file scan + 六类 mutation fixture |
| Host snapshot test | 只检查一个正向调用形状 | missing、duplicate owner 必须失败；旁路 decoy 必须不影响 verdict | exact/missing/ambiguous/decoy 4/4 |
| Host signal test | 弱分类、部分字段 | typed signal 是闭集；每个 case 的公开表示必须完整且 exact | 五 case production projection；13/13 |

## 6. 验证纪律与执行事故记录

- 一次本地验证误把 `node scripts/build.mjs` 与读取 `dist` 的 focused tests 并行执行。build 会先清空 `dist`，因此 38 个测试以 module-not-found 失败；这次运行无产品 verdict，不计为 RED 或回归证据。
- 随后严格串行：build 成功；三个 focused suite 共 316 项中 315 项通过，唯一失败为 sandbox loopback 权限。获准后单独重跑真实 Host canary 2/2。
- 第一次官方完整阶梯在 unit tier 为 3930/3932。仅两条失败：upstream 已把顶层梯子改成 `npm run format/check/owner-dep/build`，但 distribution 与 verification proof 仍要求重构前的 direct command 字符串。`1d7ca8517` 锁定顶层 npm 顺序与四个 Wireit step 的 exact underlying command；focused 17/17。该修正修改 upstream 原 proof，不修改产品语义。
- 同一 Wireit 重构只把 `scripts/build.mjs`、F# source 与 `package.json` 计入 build cache key；实际 build 还从全部 Git-tracked source/document 生成 `LoopDetectorEnvelope`。`bed2e510c` 将整个 workspace 纳入 fingerprint，只排除 `dist/` 与 `.fable-build/` 输出，并加入 executable proof；focused 11/11。该修正防止文档或新增目录变化复用陈旧 artifact。
- 第二次官方完整阶梯通过 text、FCS owner、build 与 authoritative unit 3933/3933；integration 的 `workflow-constitution-scanners.test.mjs` 随后在 185001ms 被 185000ms verdict-silence watchdog 终止，0 个 file verdict。单独执行同一文件 3/3，通过耗时 156.7 秒；三个 leaf 分别 38.3 秒、113.6 秒、4.7 秒。根因是 supervisor 只收到 file-wrapper completion，三个串行 FCS leaf 的正常总耗时被误当成一段无 verdict 静默，机器抖动后越过预算。
- `4a846be1d` 没有增加任何 timeout：将三个真实 FCS check 拆为三个顺序 file step，每项继续使用原 180000ms leaf budget。正式 integration 中分别 61.6 秒、133.3 秒、11.0 秒并各自独立续约 verdict；完整 integration、package integration 与 273/273 harness 全绿。该修正只改变验证编排，不改变 scanner、FCS oracle 或产品语义。
- 最终官方 `npm run format-build-test` 退出 0：text gate 为 772 WHAT / 3927 declarations；Fable 为 734 sources / 161 registered surfaces；authoritative unit 3933/3933；完整 integration 与 273/273 harness 全绿；Long Stroke 57 steps / 7.2s，journal 583/700、SSE 2472/3450；package integration 与 `npm pack --dry-run`（2015 files、2.2 MB packed、10.5 MB unpacked）通过。
- 完整阶梯后再次 fetch，`upstream/master` 仍为 `fcd5ab11b`，无需合并新提交。最终结果写入本文件后，只改变 LoopDetector repository envelope 输入；提交后须显式 rebuild 并重跑 text gate 与 envelope freshness proof，不能让记录提交留下陈旧 artifact。

## 7. 外部完成条件

- 本文件记录本地实现与完整阶梯事实。PR URL、GitHub CI 与 merge SHA 在实际发生后追加。
- 没有 merge SHA 前，本批仍未进入 upstream。
