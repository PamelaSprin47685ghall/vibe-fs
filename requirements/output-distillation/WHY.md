# output-distillation — 为什么必须独立存在

## 不可替代的存在理由

真实执行输出可能远大于 participant horizon（log、test run、trace、capture、dump 动辄几百 KB 到数
MB）。系统必须把「物理上发生了、输出了什么」压成「能改变后续判断的 bounded observation」。没有本包：

1. **截断冒充成功**：超限静默截成空结果，caller 以为「跑完且干净」，实际失败被吞。
2. **fragment 冒充整体**：某一个 chunk 里没有 failure 文本，被当成整次运行成功。
3. **发明因果**：合并多个 fragments 时把「许多安静 chunk」解读为「那次 failure 不真实」，或编造
   success ratio。
4. **失去可定位性**：压缩后读者找不到 path、行号、失败 assertion，蒸馏结果对没看过原文的人无用。
5. **无界缓冲**：输出预算不设防，物理采集本身变成无界资源消耗。

RED 判定：截断/压缩把局部片段伪装成整体事实，或丢失足以改变下一动作的关键信息。此时世界 RED。

## 为什么从 process-execution 独立（HANDOFF §6.6）

控制进程（command/signal/exit/cancel）与诚实压缩（有损但诚实的 observation）是两个 WHY：换
terminal backend 不动蒸馏语义；把 Distiller agent 换成 deterministic+LLM hybrid summarizer 不动
process execution contract。独立变化测试双向成立，故拆两包。

## 历史失败模式

- **「看起来干净」的空摘要**（EXEC-012）：超限走摘要策略之前，静默截断曾产生「成功空结果」。
- **fragment 越界**（Oracle 2 / HANDOFF §29）：fragment-humility 语义曾只活在 Distiller Role Law
  散文里，`raw tail verbatim 保留未断言`——行为面存在（`Distillation.fs` `partialWithTail`）但无
  可红 fixture。本包新增 `distiller-fragment-humility.test.mjs` 钉死：失败 chunk → 承认不完整 +
  保留最后 chunk 原文 + 不虚构失败者的 summary。
- **chunk 统计冒充事实**（Role Law）：把 success ratio、map-reduce 机械过程报告成蒸馏事实，是
  「切割的私务」外泄。
- **无界缓冲**（`ProcessOutput.fs` 注释）：跨 OutputLimit 瞬间一次性 dump 积压字节，曾把内存峰值
  与输出预算脱钩；`MemoryBufferBudget` 提前流式落盘。
- **JSON 当字符串叠信封**（历史 change（js-tools-toml-result））：工具结果以 `result="{\"...}"`
  字符串返回，截断语义与值树分离；TTR 后结果走值树 + ARCH-012 确定性留尾截断。

## 与相邻包的边界

- 物理采集（spool 阈值、字节计数）→ `process-execution`；本包消费 spool。
- 历史记忆/上下文压缩（Blogger、prefix、squash）→ `context-compression`（不同证据源与生命周期）。
- Reviewer 判断（PERFECT/REVISE）→ `review-judgement`；蒸馏只做「长度压力下的选择」。
- 工具文本结果 wire 上限（ARCH-012）由本包执行，但 wire 渲染 owner → `provider-projection`。
