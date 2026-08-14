# semantic-trace

> participant life 中不可丢失的原始语义历史必须有 append-only、可定位的事实表示。

## 一句话 WHY

X 的「当时到底发生了什么」必须可长期定位、可重建。若只依赖当前 transcript head 或临时摘要，
后续 review / context / recovery 会把「现在看见什么」误当「当时发生什么」。

## WHAT 概览（→ WHAT.md）

| 组 | 命题 | 保证 |
|---|---|---|
| 唯一历史 | SEMANTIC-TRACE-001/002 | XTrace 是 X 唯一 append-only 原始语义轨迹；typed capture 边界明确包含/排除什么 |
| 可定位 | SEMANTIC-TRACE-003/004/006 | cursor 严格单调；provenance 按 provider run 分段；slice/head/frontier 半开可定位 |
| 单一 source | SEMANTIC-TRACE-007 | Y delta / LWR gap / terminal capture 同源 XTrace，解析不分叉 |
| 诚实性 | SEMANTIC-TRACE-008 | 未发生材料（speculative candidate、失败 probe）永不写成历史 |
| 抗破坏 | SEMANTIC-TRACE-009/010 | Host compaction 不删 XTrace；Opening 区间在 trace 内 preserved |

## HOW 概览（→ HOW.md）

- 类型：`src/Wanxiangshu/Domain/XTrace.fs`（cursor/item/flatten/render/slice）
- durable 投影：`src/Wanxiangshu/Context/Trace/Projection.fs`（Opening/Part/Terminal 三事实，PERSIST-010 拒绝规则）
- 捕获链路：`src/Wanxiangshu/Context/Trace/Capture.fs`（唯一 `MessagePart → SemanticPart` mapper）
- fold：`src/Wanxiangshu/Composition/Durable/Fold.fs` + `XTraceProjection`

## proof 概览（→ PROOF.md）

- MOVE（已执行 Wave 2a）：`tests/unit/context/x-trace*.test.mjs`（5 文件）→ `requirements/semantic-trace/tests/`
- REUSE：`requirements/speculative-investigation/tests/**`（unpromoted ≠ history 交叉）、`requirements/durable-events/tests/fold-context-recovery.test.mjs`（durable-events fold）、`requirements/context-compression/tests/**`（blogger 收敛交叉）
- NEW：`x-trace-capture-boundary.test.mjs`、`x-trace-compaction-survival.test.mjs`、`x-trace-provider-run-provenance.test.mjs`

## 阅读顺序

1. `WHY.md` —— 为什么必须独立存在、历史上 RED 过什么
2. `WHAT.md` —— 唯一 normative 合同（编号命题）
3. `HOW.md` —— 实现模型 + 历史与弃权
4. `PROOF.md` —— 每条命题的测试落点与运行命令

## DEPENDS ON

- `durable-events`：XTrace 事实的不可变、原子提交、确定性 fold（PERSIST-010）是「可定位」的 substrate。本包只拥有语义 capture / frontier / provenance 合同，不拥有事件存储机制。

## 边界（DOES NOT OWN）

- context compression policy → `context-compression`
- provider projection / wire shape → `provider-projection`
- review judgement → `review-judgement`
- Blog/Companion 的 memory representation → `context-compression`
- 当前 XTrace type/module/file layout（可整体重写，只要 capture/frontier/provenance 合同不变）
- participant horizon admission：trace 位于 horizon 之前，只有后续 projection/record delivery 才需要该保证 → `participant-horizon`
- speculative candidate 的 promotion 因果 → `speculative-investigation`（本包只拥有「capture 侧不写未发生材料」）
