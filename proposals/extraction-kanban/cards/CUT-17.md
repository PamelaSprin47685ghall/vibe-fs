# CUT-17：请求投影与信息边界

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-17) / [证据](../evidence/CUT-17/README.md)。

## 1. 认领

- Owner：`eng-cut17c-goose`（现任；接手 `eng-cut17b-goose` 未推的本地提交）。`eng-cut16d-goose` 认领（`9242aecbc`），完成库侧 `tenure` 规则并发布 0.27.0（`c3cd45e` + `c51f35e`）后中断；`eng-cut17b-goose` 接手同一工作树 `wt/CUT-06-main` / `wt/CUT-06-wxl`（分支 `cut/CUT-17`）。
- 写集合：`src/Wanxiangshu/Mission/Planning/TenureIsolation.{fs,fsi}`；`src/Wanxiangshu/Participant/Provider/Projection/{Planner,Renderer,Surface}.fs`；wanxiangli `src/projection/`。

## 2. 处置

| 职责 | 现状核对 | 去向 |
|---|---|---|
| Plan 任期过滤（U 保留、前任交换剥离、未捕获保留、合成剥离、无 id 有语义拒绝） | `TenureIsolation.assemble` 内联 `keepMessage`/`keepsInteraction` | **抽出** → `wanxiangli/projection.tenure`（子范围①） |
| `isCurrentTenurePhysical` | 无调用者的同义规则副本 | 删除 |
| 消息解码、XTrace 首次观测游标、重锚事实写入、保留消息写回 | `PlanningSurface`、`PluginTransforms.isolatePlanTenure` | 留主仓（宿主 codec / Trace / journal） |

## 3. 子范围①：任期过滤（eng-cut17b-goose）

- B 契约：`Projection().tenure(messages, openingCursor)` → 冻结的保留下标；输入 `{ id, role, synthetic, semantic, cursor }`，游标为非负 bigint，只能属于有 id 的消息；见库 `src/projection/AGENTS.md`、`index.d.ts`，库测试 001–002。
- C 反例：前任交换混入、用户原文丢失、合成行泄漏、无 id 语义交换被编造游标或整体保留/丢弃、负游标、accessor/稀疏输入 → 库 001/002；主仓 work 034（含 “no stable XTrace message identity”）、work 020、semantic-trace 002。主线行为本来正确，无可伪造的红；以负控变异体被双算抓到作证。
- D：库 `c3cd45e`（实现 + 测试）、`c51f35e`（release 0.27.0，已在 origin/main）。
- E：gitlink `ece7176`（0.26.0）→ `351e3c5`（0.28.0，以 `c51f35e` 为祖先，已发布）；`package-lock.json` 中 `wanxiangli` 版本同步为 0.28.0。
- F：`TenureIsolation` 持有一个 `Projection()`；把 `TenureMessage` 与首次观测游标映射成纯 JS 记录交给库，按返回下标取回原始 Host 消息；`assemble` 签名与调用者（`PlanningSurface`、`PluginTransforms`）不变。负游标改由库拒绝（宿主入口 `sequenceOfJs` 本就先拒）。
- G：删除 `isCurrentTenurePhysical`、`keepsInteraction`、`keepMessage`、`validateTenure`。
- H：66 文件 242 用例 base/candidate 逐条相同；9000 次双算 0 分歧；变异体被抓到 197 次。见[证据](../evidence/CUT-17/README.md)。
