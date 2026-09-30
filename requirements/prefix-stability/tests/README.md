# prefix-stability 测试

WHAT 定义 byte identity、冷边界与呈现协议，本文只界定测试证据。

2026-09-28 上游适配：冷边界由 context-compression-028/029 的 native todowrite checkpoint 窗口拥有；WHAT 引用该所有者，不在本包重写 K 公式。009 负责 stable Host identity 写回：普通 covered assistant/tool 历史可被 LWR 替换，真实 user message 与 assume call/result 按 context-compression-017/030 原文穿透。

- 001 保留实际委托复用、guidance 连续注入和前缀判别性质。委托组中的 wire 是测试重建，已改名并单列真实 transport 采集 TODO。正反例覆盖历史各语义部分的变更。
- 002—006 检查实际 epoch/候选纯规则、同一 snapshot 渲染、rebase successor 与重复重锚。003 没有候选不等于一次真实失败；005 拒绝重放也不等于证明下一 attempt seal 前的提交顺序。缺失的 durable 流程分别留 TODO。
- 007 检查当前角色与 plan 的 prompt identity、权限；不据此声称同 Life 的 system prompt 字节稳定。008 检查确定渲染与正文分隔，low-trust 的实际呈现仍需审阅，见 GAP-107。
- 009 驱动真实 Host identity 写回及 retry 退休判定。candidate pipeline 用受控 blob port 真正物化正文、校验 digest：改变覆盖内历史会拒绝，只改变未覆盖 tail 不改变候选。CurrentProjection 来自调用方 fixture，尚未证明实际 Host 装配选对 canonical 来源；原函数名扫描已撤下。
- 010 覆盖真实 guidance 注入、原位重放、一次后缀、重锚与 journal 重开。重开仍在同一进程，名称已纠正；不声称独立进程恢复。必要的 NUL+BOM 与首轮包装来自 WHAT，而非测试新增协议。
- 011 的导出成员缺失无法证明不隐式切换 epoch 或重算时间，改为缺失证明。012 保留非法后续事实不改变投影的用例；真实 provider Failed/Aborted 后恢复仍 TODO。
- 013 验证 provider/model/variant/system/tools 改变不算追加，不替系统授权新的 epoch 来源。
- 014 将 guidance fact 的局部投影隔离与消息摄入分开。真实注入后直接 capture 的后缀泄漏反例作为失败 TODO 保存，需核对真实 Host 的输入回流边界，见 GAP-108；不能靠关键字剥离普通业务引用。
- 015 检查实际 SealRoot、memory/frame identity 对持久化输入的稳定性与区分性，并保留 snapshot 中既有 ID 的复用及真实 guidance 重入。

完成节点后先构建，再通过正式 runner 与 `TESTS_MJS_FILES` 执行本目录及相关模块。TODO 不计通过；发布节点已改为待闭合。没有执行真实 provider 或人工验收。
