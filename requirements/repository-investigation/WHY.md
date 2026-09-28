# repository-investigation — WHY

代码决策依赖当前仓库事实。记忆可能过时，搜索可能漏检，推理可能建立在错误前提上；将它们冒充观察，会让后续修改建立在不存在的证据上。可定位的来源让其他人能复核结论，也能发现事实已经变化。

调查回答“现在是什么”。为证明猜想而运行程序或改变文件，会混淆原有状态与调查制造的后果；实际执行证据由 process-execution 负责，源码修改由 repository-programming 负责。

观察的价值在于减少当前问题的不确定性。明确问题再取证，比无方向地反复搜索更有效；证据已充分后继续搜索，只增加成本和无关材料。

语义搜索适合定位候选，不适合认证事实。显式关键词让调用者控制调查方向，消费者权限限制代码暴露；有界、稳定且可失败的热启动，使辅助检索不会变成主要工作能否进行的条件。历史材料如何复用由 knowledge-reuse 负责。

## DEPENDS ON

`repository-investigation → office-capability, participant-horizon`
