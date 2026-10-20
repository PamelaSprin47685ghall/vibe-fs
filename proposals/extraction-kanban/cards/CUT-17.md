# CUT-17：请求投影与信息边界

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-17) / [证据](../evidence/CUT-17/README.md)。

## 1. 认领

- Owner：`eng-cut17e-goose`（现任；接手中断的 `eng-cut17d-goose`，其子范围②–④均已落地，无遗留 WIP）。`eng-cut17d-goose` 接手 `eng-cut17c-goose` 未推的子范围② WIP。此前 `eng-cut17c-goose` 接手 `eng-cut17b-goose` 未推的本地提交。`eng-cut16d-goose` 认领（`9242aecbc`），完成库侧 `tenure` 规则并发布 0.27.0（`c3cd45e` + `c51f35e`）后中断；`eng-cut17b-goose` 接手同一工作树 `wt/CUT-06-main` / `wt/CUT-06-wxl`（分支 `cut/CUT-17`）。
- 写集合：`src/Wanxiangshu/Mission/Planning/TenureIsolation.{fs,fsi}`；`src/Wanxiangshu/Participant/Provider/Projection/{Planner,Renderer,Surface}.fs`；wanxiangli `src/projection/`。

## 2. 处置

| 职责 | 现状核对 | 去向 |
|---|---|---|
| Plan 任期过滤（U 保留、前任交换剥离、未捕获保留、合成剥离、无 id 有语义拒绝） | `TenureIsolation.assemble` 内联 `keepMessage`/`keepsInteraction` | **抽出** → `wanxiangli/projection.tenure`（子范围①） |
| `isCurrentTenurePhysical` | 无调用者的同义规则副本 | 删除 |
| 意图规划（基底去重与冲突、插入键冲突、锚点/key 规范序） | `ProjectionPlanner.plan` 内联 `reduceBase`/`reduceInsertions`/`insertionSortKey` | **抽出** → `wanxiangli/projection.plan`（子范围②） |
| 布局（基底替换、锚点拼接、append、宿主 id/物理标记对齐） | `ProjectionRenderer` 内联 `spliceBefore`/`applyInsertions` | **抽出** → `wanxiangli/projection.layout`（子范围③） |
| 退休 attempt 的请求归属（Relayed 切点后请求是否仍属旧 attempt） | `RelayNarrativeTransform.staleRetirement` 内联 `cutToolIndex`/`afterCut`/`requestBelongsToSuccessor`/接纳根判定 | **抽出** → `wanxiangli/projection.retired`（子范围④） |
| provider 投影的规范字节（Wire 含 callId、Semantic 去 id；五种历史转义；单条消息字节供追加律） | `ProviderProjection.renderWire`/`renderSemantic`/`messageBytes` 手写 JSON（封印摘要、截止摘要、派生宿主 id 的原文） | **抽出** → `wanxiangli/projection.wire`/`wireMessage`/`semantic`（子范围⑤） |
| 消息解码、XTrace 首次观测游标、重锚事实写入、保留消息写回 | `PlanningSurface`、`PluginTransforms.isolatePlanTenure` | 留主仓（宿主 codec / Trace / journal） |

## 3. 子范围①：任期过滤（eng-cut17b-goose）

- B 契约：`Projection().tenure(messages, openingCursor)` → 冻结的保留下标；输入 `{ id, role, synthetic, semantic, cursor }`，游标为非负 bigint，只能属于有 id 的消息；见库 `src/projection/AGENTS.md`、`index.d.ts`，库测试 001–002。
- C 反例：前任交换混入、用户原文丢失、合成行泄漏、无 id 语义交换被编造游标或整体保留/丢弃、负游标、accessor/稀疏输入 → 库 001/002；主仓 work 034（含 “no stable XTrace message identity”）、work 020、semantic-trace 002。主线行为本来正确，无可伪造的红；以负控变异体被双算抓到作证。
- D：库 `c3cd45e`（实现 + 测试）、`c51f35e`（release 0.27.0，已在 origin/main）。
- E：gitlink `ece7176`（0.26.0）→ `351e3c5`（0.28.0，以 `c51f35e` 为祖先，已发布）；`package-lock.json` 中 `wanxiangli` 版本同步为 0.28.0。
- F：`TenureIsolation` 持有一个 `Projection()`；把 `TenureMessage` 与首次观测游标映射成纯 JS 记录交给库，按返回下标取回原始 Host 消息；`assemble` 签名与调用者（`PlanningSurface`、`PluginTransforms`）不变。负游标改由库拒绝（宿主入口 `sequenceOfJs` 本就先拒）。
- G：删除 `isCurrentTenurePhysical`、`keepsInteraction`、`keepMessage`、`validateTenure`。
- H：66 文件 242 用例 base/candidate 逐条相同；9000 次双算 0 分歧；变异体被抓到 197 次。见[证据](../evidence/CUT-17/README.md)。

## 4. 子范围②：意图规划（eng-cut17c-goose 起草，eng-cut17d-goose 落地）

- B 契约：`Projection().plan(intents)` → `{ ok, order }` 或显式冲突；意图视图 `{ kind, key, anchor, rows, fingerprint }`，行内容对库不透明，只比指纹；见库 `index.d.ts`、库测试 003。
- C 反例：注册顺序改变输出、同键异值被静默吞掉、基底冲突被忽略、负锚点/越界锚点处理漂移 → 库 003（200 次随机重排）；主仓 provider-projection 001–014。主线行为本来正确，以两种负控变异体被双算抓到作证。
- D：库 `9bb4288`（实现 + 测试）、`e43ef87`（release 0.34.0）。
- E：gitlink 不动（master 已是 `d2ce694`/0.35.0，含 0.34.0）。
- F：`ProjectionPlanner.plan` 签名与调用者（`ProjectionSurface`、`ProjectionRenderer`）不变；宿主把最小负锚点平移到 0 后交库。
- G：删除 `insertionSortKey`、`reduceBase`、`reduceInsertions`。
- H：双算 18000 次 0 分歧，负控 A/B 分别 1818/40 次分歧；88 文件相关测试无新增失败。见[证据](../evidence/CUT-17/README.md)。

## 5. 子范围③：布局（eng-cut17d-goose）

- B 契约：`Projection().layout(intents, baseLength)` → `{ ok, order, rows: { intent, row }[] }`；锚点为负或超出（替换后）基底长度时抛错；见库 `index.d.ts`、库测试 003。
- C 反例：基底替换后锚点空间错位、同锚点 key 序、append 次序、宿主 id 与物理标记错位 → 库 003；主仓 provider-projection 001–014。以两种负控变异体被双算抓到作证。
- D/E：库已在 0.34.0（`9bb4288`/`e43ef87`），gitlink 不动。
- F：渲染器先用 `plan` 拒绝冲突，再由 `ProjectionPlanner.layout` 交库布局，按 `{ intent, row }` 取回行。负/越界锚点的拒绝文案改为库的校验文案（无依赖者）。
- G：删除 `renderedRows`、`emptyRendered`、`spliceBefore`、`applyInsertions`。
- H：双算 18000 次 0 分歧，负控 C/D 分别 714/1116 次分歧；88 文件 verdict 不变。见[证据](../evidence/CUT-17/README.md)。

## 6. 子范围④：退休 attempt 的请求归属（LWR 交接，eng-cut17d-goose）

- B 契约：`Projection().retired({ relayed, acceptedRequest, activeRoot, gatePhysical, messages: { role, id, cut, accepted }[] })` → boolean；见库 `src/projection/AGENTS.md` LEMMA 7、`index.d.ts`，库测试 004。
- C 反例：切点仍在却放行旧 attempt；后任新根/门控 nudge/已接纳请求被误清；切点前的接纳被当作后任；最后一条 user 无 id 却被认成后任；非法输入与 accessor → 库 004；主仓 work-011 两个退休用例（宿主负控 E/F 均转红）。
- D：库 `584d06f`、`6c9845d`（release 0.39.0）。
- E：gitlink `d2ce694`（0.35.0）→ `6c9845d`（0.39.0）；`package-lock.json` 中 `wanxiangli` 版本同步。
- F：宿主只解 journal 证据、标切点、交库裁决；`isStaleRetiredAttempt`/`apply` 签名与调用者（`PluginTransforms`、`Mission.Work.ProjectionSurface`）不变。
- G：删除 `messageRoleIsUser`、`cutToolIndex`、`afterCut`、`requestBelongsToSuccessor` 与内联判定。
- H：90 文件 392 条 verdict base/candidate 逐条相同；宿主负控 E/F 各抓到 2 个用例；规则级双算 60000 次 0 分歧、三种变异均被抓到。见[证据](../evidence/CUT-17/README.md)。

## 7. 子范围⑤：provider 投影的规范字节（eng-cut17e-goose）

- B 契约：`Projection().wire(view)` / `wireMessage(message)` / `semantic(view)` → string；视图 `{ provider?, model?, variant?, tools, system, messages: { role, parts }[] }`，部件 `text|reasoning|tool-call|tool-result|media`；线格式 tool-call/tool-result 必须带 callId，语义部件带 callId 即拒绝；字段顺序与五种转义（`\\` `"` `\n` `\r` `\t`，其余字符原样）固定；见库 `src/projection/AGENTS.md` AXIOM 5、LEMMA 8–9、`index.d.ts`，库测试 005。
- C 反例：Semantic 与 Wire 混用（语义带 callId / 线格式缺 callId）、缺省与 null 渲染漂移、改用标准 JSON 转义（会改变已存封印摘要）、字段顺序漂移、非字符串/未知 kind/稀疏/accessor → 库 005；主仓 provider-projection 004（“canonical wire rendering freezes the generic row shape”）。主线行为本来正确，以两种负控变异体被双算抓到作证。
- D：库 `cf517e7`（实现 + 测试 + 文档）、`9334267`（release 0.41.0）。
- E：本刀不改 gitlink、lockfile：落地前 master 已由 CUT-11 前进到 `d0318dc`（0.42.0），其祖先含 `9334267`（0.41.0）。
- F：`ProviderProjection.renderWire`/`renderSemantic` 签名与全部调用者（`ProjectionSurface.sealDigest`/`renderWire`/`renderSemantic`、`ProjectionRenderer.cutoffDigest`、`ProjectionMessageEdit` 派生宿主 id、`Context/Trace`、追加律 `isAppendOnlyPrefix` 的逐条字节）不变；宿主只把已类型化的部件降成普通视图交库。
- G：删除宿主自有的 `quote`/`optional`/`jsonArray`/`field`/`jsonObject`/`wirePartJson`/`semanticPartJson` 与内联 `messageBytes` JSON。
- H：501 条 verdict base/candidate 逐条相同；另 23 个触及封印/截止/语义轨迹的文件 426 pass / 0 fail；双算 54000 次 0 分歧；负控 G/H 分别 6252/10461 次分歧。见[证据](../evidence/CUT-17/README.md)。
