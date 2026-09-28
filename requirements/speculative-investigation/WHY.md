# speculative-investigation — WHY

主模型常要串行读取文件、搜索符号后才能判断任务。提前完成少量机械只读调查可能降低等待与成本，但预测错误不能改变权威世界，也不能让业务依赖这项优化。零影响基线、请求预算和只读边界把失败限制在一笔投机成本内。

提前取得的材料不等于已发生的主会话历史。Prepared 先保证待消费材料可恢复，Promotion 再由真实消费证据确认因果归属，重放和 XTrace 将已确认的材料保持在正确位置。这使优化结果既不能提前污染历史，也不能在被使用后丢失。

Replica 继承 Owner 身份和语义视界，避免额外角色提示改变调查行为；审计保留在模型之外。Shadow、Control 与 DryRun 提供评估证据，区分自然发生的主请求与投机自身的干预，避免用自己制造的结果证明自己有效。生命周期按因果事件结束，保持复现性；观察工具的监督时限不应替代业务规则。

## DEPENDS ON

`repository-investigation`, `participant-identity`, `execution-model-routing`, `participant-horizon`, `provider-projection`, `semantic-trace`
