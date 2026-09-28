# concern-routing 测试与证明范围

规则来自 WHAT；本页不增加规则。pure projection、真实插件和物理 Host 是不同证明范围。TODO 不计通过。

| 文件 | 已执行的边界 | 尚缺的边界 |
|---|---|---|
| 001 | 生产 subscribe/fold 的空输入、owner/语义冲突、generation 身份、幂等 | 两个真实 workspace 隔离、持久并发 claim |
| 002 | per-recipient coverage；实际插件中新加入的 Engineer 获公告一次，收不到 owner 私信 | 全部 eligible/ineligible 角色、重启后的公告 |
| 003 | exact generation/occurrence 的生产fold；实际 publish 入口的重放/冲突 | 真实退休与 append 竞争、持久恢复 |
| 004 | pure placement 全有或全无；实际插件冻结旧 Pair Hint、新 occurrence 收新消息、之后不再重复 | 同一持久提交的失败/放弃/进程崩溃与重开 |
| 005 | 实际 subscribe/publish 不改变双方 PromptAuthority 观察、不创建/提示/abort Host 会话 | 完整 obligation 与 office 投影、交付后的模型证据判断 |
| 006 | 手动退休后的拒绝、不可变 concern、新代公告、旧材料不穿代 | 实际 participant 终结驱动退休；已有失败 TODO，见 GAP-157 |
| 007 | 待真实职责边界审计 | 禁词扫描不能证明没有工作流或新权威 |

插件用例运行真正工具、共享 journal 和 messages transform，Host/provider 端口由正式 fixture 提供；没有真实 provider 调用。提示内容从正式 NUL+BOM 后缀观察，不能沿用已废止的 synthetic-message 假设。普通 transform 可能启动 Blogger；“publish 不打断 owner”观察 owner 的物理请求，而不是禁止所有合法旁路工作。

GAP-156 修复入口把任意同 occurrence 当作成功重放的问题，保留完整 sender/address/message 一致性。GAP-155 记录缺证，GAP-157 记录真实生命周期反例；两个发布节点改 TODO，不能从8项旧unit测试推断全包完成。

新基线验证范围见[本批记录](../../../proposals/35模块PR施工记录-2026-09-28.md)。真实崩溃和普通安装场景仍需人工巡检后续证据。
