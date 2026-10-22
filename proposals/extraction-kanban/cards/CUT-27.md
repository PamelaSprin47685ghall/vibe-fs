# CUT-27：Casebook / 知识复用去向 —— 裁决

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-27)。

- **裁决人**：eng-cut19-goose，2026-10-20，按仓主“完全自主决定”的授权。
- **裁决**：**保留产品功能，分步抽取**。不删除（knowledge-reuse WHAT 001–016 仍生效，`requirements/knowledge-reuse/tests` 有 16 个编号测试），也不整体 RETAINED。

## 依据（亲眼核对，master 17ed19c67 之后）

- 主仓 `src/Wanxiangshu/Repository/Knowledge/Casebook/` 共 24 个 `.fs`、4028 行；真实消费者：`OpenCode/Tools/FetchTool.fs`（`CasebookIndex.resolve`）、Bookkeeper 刷新链（`Bookkeeper.fs` → `CasebookIndex.refresh`）、Capture/Settlement 写入 EventStore。
- 持久权威在统一 EventStore（WHAT 007），多 writer 合并与保留已在 CUT-19 子范围①②交给 `wanxiangli/convergence`/`retention`；Casebook 不得另立存储。
- 草稿“不再建”与仍生效的 WHAT 冲突（D-05），按 D-05 默认保留并维护；没有删除授权的证据，故不选 REMOVED。

## 抽取子范围（按依赖从内到外，每步可独立验收）

1. **Index 纯投影**（READY）：`CasebookIndex` 中的 `compactTitle`、`shelfmarkFor`（FNV-1a 判别后缀）、从 Case 列表到 `{shelfmark, question}` 的 LRU 截取与 epoch 推进判定，抽为纯函数；进程级 `frozen/dirty` 单例与 EventStore 读取留在主仓组合者。反例：同问不同会话 shelfmark 撞车；Case 顺序变化但公开索引不变时 epoch 不应推进；内部 sessionId 泄漏到公开条目。
2. **Fetch 差异决策**：基线 B 与目标 T 的 diff 分类（无差异 / 有差异需 CaseRefresh / 读取故障不等于 Missing，WHAT 005/016）。
3. **Capture 实质访问判定**（WHAT 003）：工具结果 → relatedPaths 的纯分类。
4. Bookkeeper（LLM 参与者）、Settlement/fatal（WHAT 013）、Host 工具接线留在主仓，最后随 CUT-35 组装评估。

## 不做

- 不改 Casebook 事件词汇与 NDJSON 字节；不迁移已有 Case 数据。
- 不在本卡删除任何 Case 原件或缓存文件。

## 当前状态

**DONE（2026-10-21，eng-cut27d-goose 收尾）**。子范围①②③均已合入 master（§1、§2、§3）；④按裁决明确留主仓（RETAINED，理由见“抽取子范围”第 4 条），随 CUT-35 组装评估，不藏在本卡 DONE 之下。既有的 7 个 knowledge-reuse-010 失败在 base 与候选同名同因，不是本卡引入，也未由本卡修复。

## §1 子范围① Index 纯投影交给 `wanxiangli/casebook`（eng-cut19d-goose 起草，eng-cut27b-goose 接手落地，2026-10-21）

- **库**：`wanxiangli/casebook` / `CasebookIndexLaw`（库提交 `654389f`，随 `ece7176` 发布 0.26.0）：`compactTitle`、`shelfmarkFor`（判别后缀逐位冻结旧算术，已发出的 shelfmark 仍可寻址）、`entries`（公开条目只有 `shelfmark`/`question`，按 shelfmark 码元序稳定排序）、`locate`（同名取第一个，找不到 -1）、`advancesEpoch`（无上一快照 / dirty / 公开条目逐项含次序变化）、`evictionVictims`（LRU）。库测试 `src/casebook/tests/{008,012}.test.mjs`：160 组冻结向量与主仓 base 构建逐字相等；淘汰与参考实现固定种子随机输入逐项相等（number 与 bigint 访问序）。
- **主仓**：`CasebookIndex` 只剩进程级 `frozen/dirty` 单例、EventStore 投影读取与 epoch 数值；`CasebookProjection.evict` 只按库给的名单删除。删去手写 `compactTitle`、`fnv1aDigest`、`resolvedEntries`、`visibleChange`、`epochAfterRefresh` 与 LRU 选择。
- **反例（真实失败，旧实现红）**：标题 71 码元截断落在代理对中间时，旧实现发出含孤立代理的 shelfmark；经 UTF-8 传输（provider 线路）回传后变成 U+FFFD，`resolve` 找不回刚展示的 Case。新增 `knowledge-reuse/tests/012` 用例 `CASEBOOK_shelfmark_of_a_long_title_survives_utf8_and_still_resolves`：base 红（`logs-cut19d/cb-base-red.log`，探针 `cb-base-probe.json`：`wellFormed:false, viaUtf8:null`），候选绿。这是有意的行为改变：只影响原本就无法回传寻址的 shelfmark。
- **影子双算**（`evidence/CUT-27/shadow-index.mjs`，临时夹具 EventStore，不碰真实 `.git/wanxiangshu`）：300 轮，base dist（master 1c43198b5 构建）与候选 dist 各自驱动同样的 archive/invalidate/refresh/resolve 序列。shelfmark 5814 相等 + 186 为上述代理对截断（base 非良构、候选良构）；LRU 300/300 相等；刷新步 1425 相等 + 375 含代理对截断条目；**无法解释的差异 0**。rebase 后在 master 13944a5db 上复跑结果相同（`logs-cut27b/shadow.json`）。
- **验证**（rebase 到 master 13944a5db，gitlink ece7176 / 0.26.0，`package-lock.json` wanxiangli 版本行 `0.25.1 → 0.26.0`，`node scripts/build.mjs` ok，fantomas --check 两文件通过）：knowledge-reuse 16 文件 135 pass / 7 fail / 10 todo；base 134 pass / 8 fail，多出的 1 个即上面的新用例；其余 7 个 knowledge-reuse-010 失败 base 与候选同名同因（既有）。managed-session-lifecycle-022 3 pass / 2 fail，与 base 日志同名同因。`node scripts/check.mjs` 按“文件+规则”归一后与 base 的差异只在 `PluginHooks.fs`/`StaticTools.fs`（本刀未触及，来自其间其它卡的提交）；本刀文件无新条目。日志 `logs-cut27b/{build,cand-run,msl022,check-cand}.log`。
- **回退**：纯计算，不迁移数据、不改事件词汇；`git revert` 主仓提交即回到手写版本（库 0.26.0 可保留）。
- **未做**：全量 `scripts/verify.mjs` 与 integration tier。

## §2 子范围② Fetch 差异决策交给 `wanxiangli/casebook-maintenance`（eng-cut27b-goose 起草，eng-cut27d-goose 落地，2026-10-21）

- **库**：`CasebookMaintenanceLaw`（库提交 `92ce268`，随 `1b83d0d` 发布 0.29.0）：B→T 零上下文逐文件 diff（Myers 二分与渲染）、刷新裁决（无差异 / 需 CaseRefresh）、只有 ENOENT 才算 Missing（其它读取故障不等于 Missing，WHAT 005/016）、预算截断。库测试 `src/casebook-maintenance/tests/005.test.mjs`，夹具 `tests/fixtures/host-diffs.json` 为主仓旧 `computeMaintenanceDiff` 生成的 400 组黄金向量。
- **主仓**：master `dc23ec407`。`CasebookCapture` 只剩文件读取、载荷写入与基线解码；手写 diff/裁决/截断删除（Capture.fs 净删约 300 行）。
- **黄金向量**（`evidence/CUT-27/golden-diffs.mjs`，临时工作区，不碰真实 `.git/wanxiangshu`）：在落地后的候选 dist 上重生成 400 组（329 有差异、59 多文件），与库夹具逐字节相等 400/400；旧 base dist（`.cut19d-base-dist`，master 1c43198b5 构建）同样 400/400 相等。
- **反例（真实失败，旧实现红）**：预算截断落在代理对中间会留下孤立代理，经 UTF-8 传给 Bookkeeper 变成 U+FFFD。新增 `knowledge-reuse/tests/014` 用例 `truncation never cuts a code point in half`；探针 `evidence/CUT-27/probe-027.{mjs,txt}`：base 在 40..79 的 40 个预算里有 30 个产出非良构或超长文本，候选 0 个。有意的行为改变，只影响原本已损坏的截断输出。

## §3 子范围③ Capture 实质访问判定交给 `wanxiangli/casebook-access`（eng-cut27c-goose 起草，eng-cut27d-goose 落地，2026-10-21）

- **库**：`CasebookAccessLaw`（库提交 `5026f6e`，随 `a3664e2` 发布 0.31.0）：哪些工具结果/执行器读取算实质访问（WHAT 003）、变更仅在 committed 后计入、move 两端都计、发现类（grep/glob/ls）永不计、relatedPaths 规范形（去空白、去重、按码元排序，裂变泳道并集）。库测试 `src/casebook-access/tests/003.test.mjs`（SPEC 1–4）。
- **主仓**：master `699609cfd`。`AccessTracker` 只剩按会话累加器；分类与规范化来自库。
- **影子双算**（`evidence/CUT-27/shadow-access.{mjs,json}`，纯计算，固定种子 2703）：3000 轮、16868 个观测，16809 相等，59 不同，**全部**为执行器命令含反引号命令替换（`ofExecCommand:backtick`）——旧实现把 `` `x` `` 或其后的词当成文件名记为 FileRead，候选不记（命令替换不是文件，库 SPEC[casebook-access-4]）。工具结果分类与 relatedPaths 0 差异。另有探针：`head -c 100 f`/`tail -n +5 f`/`tail -c 20 f` 旧实现把选项值（`100`/`+5`/`20`）当成文件名，候选记为 `f`。两者都是有意修复，只影响原本记错路径的情况；无法解释的差异 0。
- **验证（两刀一起，rebase 到 master 943f8f66e，gitlink d2ce694 / 0.35.0 不变，lockfile 0.35.0 一致）**：`node scripts/build.mjs` ok；knowledge-reuse 16 文件 136 pass / 7 fail，7 个均为既有 knowledge-reuse-010（与 `logs-cut19d/cb-base-run.log` base 同名同因）；推送前检查（lockfile JSON、gitlink 在库 origin/main 上且不早于 master、版本一致）通过。日志 `/workspace/vfs-cut/wt/logs-cut27d/land-1.log`。
- **回退**：纯计算，不改事件词汇与 NDJSON 字节、不迁移数据；`git revert 699609cfd dc23ec407` 回到手写版本（库版本可保留）。
- **未做**：全量 `scripts/verify.mjs` 与 integration tier；`managed-session-lifecycle-022` 未在本次复跑（§1 记录其 2 个失败与 base 同名同因）。
