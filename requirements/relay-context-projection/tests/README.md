# relay-context-projection 测试证明范围

新上游保留完整物理历史，退休 cut 只判定请求身份。旧“删除前任消息”的合同和反例不再适用；没有把旧过滤实现迁回。

`support/cut.mjs` 通过真实插件和journal执行 REVISE→suicide→退休请求transform，取得真正owner派发的后继prompt。随后 `ProjectionSurface.apply` 只转换输入、观察interrupt回调，并调用生产 `RelayNarrativeTransform.apply`，没有复制投影算法。

- `001/005/006`：在这条真实前序上检查历史保留、同物理session和确定性，完整比较可见消息及输入不变。
- `002/008`：同一durable退休事实下，旧请求、伪装成user的未准入wake会被拦截，实际后继gate可通过；wake作为历史仍保留。文本包含工具名不能取得权威。
- `003/004/007/009`及其余TODO：全链秘密/控制标识、Agent独立调查、进程崩溃恢复、Accepted证书失效重开、每次authority修订和DevOps执行事实仍未证明。

上游 `projectMessages` 仅回传输入；保留该兼容API，但所有本包活动测试已停止把它当证据。新接缝的interrupt回调只记录实际选择，不证明真实Host进程已终止；完整插件入口的Continue/Accepted观察另见relay-retirement-008。

旧GAP-195仍需按新证明对象重审；GAP-196所指旧cut删除后继输入的问题因上游取消消息过滤已不再是同一个合同反例，不能把旧失败转称新实现通过。当前对应轨迹保留为新测试，等待新构建。

本轮活动mjs语法通过，行为测试未执行。独立Fable编译被上游闭包依赖/顺序阻断，详见 [迁移记录](../../../proposals/archive/2026-10-03/20模块迁移-Sphinx与Manager-2026-09-28.md)。
