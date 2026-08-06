# Journal — 理由

先写盘后改内存：内存会看见无证据的未来。截断而非跳过中间损坏：事件前后相扣，缺中间则后续建立在错基上。

O(1) projection：完整扫描把「查询」变成「重放成本」，恢复路径不可控。Requested→Accepted 把外部副作用做成可审计意图，而不是「内存里好像做过了」。

上下文事实 fold 的原子性（尤其 BlogEntry 与 ContextReanchored）防止「只改一半投影」的撕裂世界。
