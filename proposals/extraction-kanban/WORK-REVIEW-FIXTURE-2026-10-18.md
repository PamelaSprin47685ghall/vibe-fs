# CUT-00-R4：Work评审夹具的真实输入

R7-B扩大组发现work011五个普通失败。非作者将同一原样文件及fixture用于旧6cc27精确包、当前0d60精确包和当前工作区，三路均0pass/5fail/1TODO、exit1、无timeout；失败名称相同。产品四个相关模块的摘要相同，排除只换wanxiangli0.6.8才引入这些失败。证据`/private/tmp/work011-attribution-evidence-S5hPQP/`，实际依赖借用范围已明示，不称完全仓外独立消费。

## 已确认的三项夹具错误

1. review ToolPart没有Host持久part.id，生产快照在进入kernel前就拒绝绑定。补真实id后两包分别3pass/2fail/1TODO，self-review确实进入kernel并拒绝owner；最终同时填写SDK的sessionID/messageID，归属与实际消息一致，不放松生产绑定。
2. Promoted例将已捕获review-run、retirement-run同一身份的原文改成占位文本，触发正确的原件身份冲突。改为实际Host消息的副本，保留原件校验与原有交付断言。
3. Stayed helper把Host double的flat prompt直接交给SDK transform；该入口需要info/parts。现在先保留完整历史，执行retired-attempt transform，确认实际successor派发后才按SDK封装gate，并断言历史中没有该精确gate身份。

## 本轮新增错误与review撤回

非作者早期诊断用filter排除“最后消息”，当时实际删掉retirement-run，所报两包5绿不能证明历史正确，应撤回该阶段归因。原始三路5红、仅id的3/2仍有效。作者第一次把gate提取提前到transform前，破坏真实派发顺序，全Work中1项失败；`/private/tmp/work011-fixture-work-green.log`虽名称含green，实际exit1，不能计通过。

已删除filter和提前提取，保留完整retirement历史，等实际派发后才取gate。最终非作者读取shared源、检查原件/顺序并独立011：5pass/0fail/1TODO、exit0、无timeout；三文件运行前后hash相同，`/private/tmp/work011-final-review-evidence-iMfFDw/summary.json`。这是最终证据，不采用错误诊断的5绿替代。

## 验收与边界

Work全目录160项：159pass/0fail/0skip/1TODO，`/private/tmp/work011-fixture-work-final.log`。正式011为5pass/0fail/1TODO、完整文件完成；正式门禁因既有TODO而exit1，不能记正式验收通过，`work011-fixture-formal-final.log`。首次正式入口只因未刷新生成输入拒跑，另留原日志，不算业务执行。构建632后freshness及0d60精确产物closure不变已核对，`work011-fixture-final-fresh.log`。

只改011和两个私有测试helper，未改产品、库、vendor或用户数据；R7-B主包0d60产品字节不变。原有in-flight Host中断TODO未实现，不把fixture通过写成整个work011已完成。没有部署Host；R12-U1继续暂缓。
