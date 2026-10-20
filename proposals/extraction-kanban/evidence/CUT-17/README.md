# CUT-17 证据

返回 [卡片](../../cards/CUT-17.md)。全部在共享机 `wt/CUT-06-main` 上实际运行；未跑全量 `scripts/verify.mjs` 与集成 `run.mjs`（共享机内存紧，按 PROTOCOL 只跑相关测试）。

## 子范围①：Plan 任期过滤（wanxiangli/projection `tenure`，0.27.0 发布，接线用 0.28.0）

- base = master `be5330991`（gitlink `ece7176` / 0.26.0）；candidate = 本提交（gitlink `351e3c5` / 0.28.0，`TenureIsolation.assemble` 改由库裁决）。
- 测试集：`tenure-suite.files`（participant-horizon 001–015、semantic-trace 002、work 001–050，共 66 文件 242 用例）。
- 结果：base 与 candidate 均为 221 pass / 9 fail / 1 skipped / 11 todo，224 条 verdict **逐条完全相同**（`tenure-suite.verdicts`，`diff` 空）。9 个失败（work 009/011/019×3/020/021/024/026）在 base 上同样失败，为既有问题，与本刀无关。
- 产物：`dist/Mission/Planning/TenureIsolation.js` 第 4 行 `import { Projection } from "wanxiangli/projection"`。
- 双算（`tenure-diff.mjs`）：经公开面 `PlanningSurface.assembleTenureMessages`，旧实现取 `wt/CUT-08-main/dist`（其 `TenureIsolation.fs` 与 master 本刀前同源，`git log` 核对），新实现取本工作树 dist；3 个种子 × 3000 轮随机 Host 消息（含无 id、合成、空 parts、未知角色、游标缺失）共 9000 次比较 **0 分歧**，其中约 2000 次双方都以 “no stable XTrace message identity” 拒绝。
- 负控：把库 `keepsInteraction` 的 `cursor >= opening` 改成 `>`，同一双算 3000 轮抓到 197 次分歧（`tenure-diff.txt` 末行）；恢复后 0 分歧。第一次负控未抓到是因为最初的 LCG 低位相关、从未生成“游标恰等于开场”的样本——已换成 mulberry32 后重跑，上面数字均为换后结果。

### 子范围① 变基复核（eng-cut17c-goose，落地前）

- 原提交 `1788e8f41` 中断未推；变基到 master `0ba124c4a`（其间落地 CUT-09、CUT-22、任务书A 文档等）后重建。
- base = master `0ba124c4a` 全新构建（`wt/CUT-17-base`）；candidate = 变基后本提交全新构建。同一 66 文件：两边均 223 pass / 8 fail / 1 skipped / 11 todo，225 条 verdict **逐条相同**（`tenure-suite.verdicts` 已按此次 candidate 重写）。与首次记录相比多出一条 work-020 新用例、work-020 旧失败转绿，两边同样如此，来自 master 其间的提交，不是本刀。
- 双算 `tenure-diff.mjs`（旧实现改指 `wt/CUT-17-base/dist`）3 种子 × 3000 = 9000 次 **0 分歧**，与首次数字一致。
- 再次变基到 master `03295f1fd`（落地 CUT-10 路由/许可/容量与 work 测试迁移，gitlink → `d2ce694`/0.35.0）后重建 candidate：同一 66 文件 233 pass / 2 fail / 1 skipped / 11 todo；2 个失败均在上面 base 的 8 个失败之内（其余 6 个由 master 其间的测试迁移转绿），无新增失败；双算 9000 次仍 0 分歧。本刀不改 gitlink。

## 子范围②：ProjectionPlanner.plan（wanxiangli/projection `plan`，0.34.0 发布，gitlink 仍为 master 的 `d2ce694`/0.35.0）

- 由 eng-cut17c-goose 写成本地 WIP 后中断，eng-cut17d-goose 接手、变基到 master `981008460` 后全新构建并核对。本刀不改 gitlink、lockfile。
- 宿主改动：`Planner.fs` 只把每条意图翻成 `{ kind, key, anchor, rows, fingerprint }`（指纹 = 行内容含宿主 id/物理标记的嵌套数组 JSON），交给库 `plan`，再按库返回的 `order` 取回原意图；冲突原样映射回 `ProjectionConflict`。宿主锚点可为任意 int（越界由 `ProjectionRenderer` 拒绝），库只收非负锚点，故把最小锚点平移到 0——plan 只比较锚点的先后与相等，平移不改变答案。删除宿主自有的 `insertionSortKey`/`reduceBase`/`reduceInsertions`。
- 双算（`plan-diff.mjs`，`plan-diff.txt`）：经公开面 `ProjectionSurface.plan` 与 `renderMessagesWithHostIds`，旧实现取 `wt/CUT-17-base/dist`（master `0ba124c4a`，其 `Participant/Provider/Projection/` 与当前 master 逐字节相同，`git diff --stat` 为空），新实现取本工作树 dist；随机意图含重复/冲突基底、同键同值与同键异值插入、负锚点与越界锚点、append、含引号/逗号的文本。3 种子 × 3000 轮 × 2 个面 = 18000 次比较 **0 分歧**（约 1/2 规划冲突，约 3/4 渲染以越界抛错，双方一致）。
- 负控：在候选 dist 的拷贝上 A）去掉锚点平移 → 1818 次分歧；B）插入意图指纹置空 → 40 次分歧。两者都被抓到。
- 相关测试（`planner-suite.files`，88 文件：上面 66 文件 + provider-projection、prefix-stability、participant-horizon 全部、interaction-authority 008、host-boundary 020）：candidate 375 pass / 2 fail / 8 cancelled / 1 skipped / 20 todo（`planner-suite.verdicts`）。base 取 `wt/CUT-17-base`（master `0ba124c4a` 的 dist 与测试）：365 pass / 8 fail / 8 cancelled；差异（`planner-suite.base-vs-cand.diff`）全部来自 master 在 `0ba124c4a` 之后对 work 009/019/021/024 测试的迁移（失败转绿与新增用例），**没有 base 通过而 candidate 失败的用例**。prefix-stability-001 的 8 条在两边同样失败（文件级取消），work-011/026 两边同样失败，均为既有问题。
- 产物：`dist/Participant/Provider/Projection/Planner.js` import `wanxiangli/projection` 的 `Projection` 并调用 `projection.plan`。

## 子范围③：ProjectionRenderer 布局（wanxiangli/projection `layout`，0.34.0 已含，gitlink 不动）

- 宿主改动：`ProjectionRenderer.renderMessagesWithHostIds` 仍先用 `ProjectionPlanner.plan` 拒绝冲突（同一 `invalidOp` 文案），随后由新增的 `ProjectionPlanner.layout` 把原始锚点（不平移）与基底长度交给库 `layout`，按库返回的 `{ intent, row }` 取回行、宿主 id 与物理标记。删除宿主自有的 `renderedRows`/`emptyRendered`/`spliceBefore`/`applyInsertions` 及按降序拼接的规则。
- 双算（`layout-diff.mjs`，`layout-diff.txt`）：旧实现 `wt/CUT-17-base/dist`，新实现本工作树；锚点集中在基底（或替换基底）范围附近，含 -1、越界、append、同键同值重复、重复基底。经 `renderMessagesWithHostIds` 与 `renderMessages` 两个面，3 种子 × 3000 轮 = 18000 次比较 **0 分歧**：约 7400 次双方逐字节相同地渲染成功，其余双方都拒绝。
- 行为差异（如实记录）：拒绝时的**异常文案**变了。冲突拒绝仍是宿主自己的 `requires a conflict-free intent set`（双算里逐字核对）；锚点为负或越界时，旧文案来自 `List.splitAt` 内部（`The input must be non-negative` / `insufficient number of elements`），新文案来自库的锚点校验（`intents[i].anchor … past the end of the message base` / `must be null (append) or a nonnegative …`），能指出哪条意图出错。同时有负锚点与越界锚点时，旧实现因按降序拼接先撞到越界，新实现先报负锚点——两边都拒绝。没有调用者或测试依赖旧文案（相关测试 verdict 不变）。因此子范围②的 `plan-diff.mjs` 在本刀后对 render 面报出约 600 次/种子的“分歧”，逐条核对全部是“双方都抛错、文案不同”，plan 面仍 0 分歧。
- 负控（候选 dist 拷贝）：C）基底行报告为物理行 → 714 次分歧；D）意图内的行倒序 → 1116 次分歧。
- 相关测试（同 `planner-suite.files` 88 文件，增量构建 generation 31）：375 pass / 2 fail / 8 cancelled / 1 skipped / 20 todo，verdict 与子范围② candidate **逐条相同**（`layout-suite.verdicts`）。
- 生产调用面核对：`ProjectionIntent`/`ProjectionRenderer.renderMessages*` 在 `src/` 中只被 `ProjectionSurface`（纯 JS 契约面，供 provider-projection 测试与集成 harness 使用）调用；`XWireSurface` 只用 `cutoffDigest`。

## 子范围④：退休 attempt 的请求归属（wanxiangli/projection `retired`，0.39.0 发布，gitlink `d2ce694`/0.35.0 → `6c9845d`/0.39.0）

- 库：`584d06f`（`retired()` 实现 + 库测试 004，9 个用例）、`6c9845d`（release 0.39.0，已在 origin/main）。projection 全部 25 个库测试 + exports/contracts 5 个通过。
- 宿主改动：`RelayNarrativeTransform.staleRetirement` 只在有 Relayed 且带投影切点的退休时，从 journal 取出证据（当前逻辑运行根、该退休门控 nudge 的接纳物理消息、每条带 id 消息的 ChatExecution 接纳根）、按消息标出是否带切点工具 call/result，交库 `retired` 裁决。删除宿主自有的 `messageRoleIsUser`、`cutToolIndex`、`afterCut`、`requestBelongsToSuccessor` 与内联的 successor/acceptedRun 判定。空白根/物理 id 交库前记为 null：宿主消息 id 从不为空白（`ProviderWireDecode.hostMessageId`），空白 id 本来就匹配不上，语义不变。
- 相关测试（`retired-suite.files`，90 文件：上面 88 文件 + host-boundary 019、durable-events 027；work 001–050 全部在内）：base（master `2eab66e14` 的 dist）与 candidate 均为 378 pass / 2 fail / 8 cancelled / 2 skipped / 20 todo，392 条 verdict **逐条相同**（`retired-suite.verdicts`）。
- 宿主负控（在候选 dist 上就地改、跑完复原并 `cmp` 核对）：E）`retired` 恒为不过时 → work-011 两个用例转红（“post-cut wake after a fresh successor root is cleared…”、“actual Stayed deliver … the next transform stops the retired attempt…”）；F）恒为过时 → 同样两个 work-011 用例转红。真实的宿主路径确实经过本规则。
- 规则级双算（`retired-diff.mjs`，`retired-diff.txt`）：把 master `2eab66e14` 的 F# 判定逐行转写成 JS（journal 查询已解成证据），与库 `retired` 在随机请求上比较（角色含大小写变体与 null、id 含 null、切点、接纳根、门控）。3 种子 × 20000 = 60000 次 **0 分歧**（约 2/3 判为过时）。对转写一侧做三种变异（切点后改成 `>=`、取第一条而非最后一条 user、忽略切点是否仍在）分别抓到 498 / 2131 / 276 次分歧。注意：这一层的旧实现是转写而非编译产物，主证据是上面的宿主测试与宿主负控。
- 已知边界差异（如实记录）：消息角色为非字符串时，旧实现只在从末尾向前扫到第一条 user 之前碰到它才会抛 TypeError，新实现由库对整批消息校验后拒绝；宿主 codec 产出的角色总是字符串或缺失，此差异只在畸形输入上出现。

## 子范围⑤：provider 投影的规范字节（wanxiangli/projection `wire`/`wireMessage`/`semantic`，0.41.0 发布；落地时 master gitlink 已是含它的 `d0318dc`/0.42.0，本刀不动 gitlink）

- 库：`cf517e7`（实现 + 库测试 005，5 个用例）、`9334267`（release 0.41.0，已在 origin/main）。projection 全部 30 个库测试 + exports 3 个通过。
- 宿主改动：`ProviderProjection.renderWire`/`renderSemantic` 与追加律用的单条消息字节，改为把类型化部件降成 `{ kind, … }` 视图交库，删除宿主自有的手写 JSON 渲染。`toSemantic`（Wire → Semantic 的唯一单向降级）与两种类型仍在宿主，因为 `Context/Trace` 直接以这两个类型取材。
- base = master `b77ebae58` 全新构建（gitlink `6c9845d`/0.39.0）；candidate = 本提交（库换成 `cf517e7`，与 release `9334267` 只差版本号）。测试集 `bytes-suite.files`（上面 90 文件的超集，112 文件）：两边均 474 pass / 2 fail / 8 cancelled / 2 skipped / 33 todo，501 条 verdict **逐条相同**（`bytes-suite.verdicts`）。2 fail 与 8 cancelled（prefix-stability-001 文件级）为既有问题。
- 附加集 `bytes-extra.files`（23 个调用 sealDigest/cutoffDigest/renderWire/semantic-trace/ProjectionMessageEdit/Seal 等、不在上面集合内的文件）：candidate 426 pass / **0 fail** / 21 skipped / 8 todo（未对 base 重跑：没有失败，故无“新增失败”可言）。
- 双算（`bytes-diff.mjs`，`bytes-diff.txt`）：旧实现 `wt/CUT-17-base/dist`（其 `Model.js`/`Surface.js` 与 base master 的 `Model.fs`/`Surface.fs` 同源，`git log 0ba124c4a..b77ebae58` 对这两个文件为空），新实现本工作树 dist；经公开面 `sealDigest`（恒等哈希，即完整线格式字节）、`renderWire`、`renderSemantic`、`semanticallyEqual`、`isAppendOnlyPrefix`、`cutoffDigest` 六个面，随机字符串含五种转义字符、其他控制字符、U+2028、emoji、孤立代理与空串，provider/model/variant 含 null。3 种子 × 3000 轮 × 6 面 = 54000 次 **0 分歧**。
- 负控（在库 dist 上就地改、跑完复原并 `cmp` 核对）：G）不转义 `\t` → 6252 次分歧；H）null 渲染成空串 → 10461 次分歧。宿主测试层：H 使 provider-projection-004 转红；G 未被任何宿主测试抓到（宿主测试没有带 tab 的样本），由库测试 005 与双算覆盖——如实记录。

- 落地前变基复核：两次变基（CUT-11 `db1b5ff60` 0.40.0、work 测试 `fc016500b`、CUT-11 `578fa6e7e` 0.42.0）。每次带入源码或库变化后全新构建重跑同一 112 文件，verdict 与首次 candidate 逐条相同；只改测试的 `fc016500b` 单独重跑其 work 026/049（7 pass / 0 fail）。
