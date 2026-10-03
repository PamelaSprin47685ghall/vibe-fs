# context-compression 测试

WHAT 拥有输入、提交与生命周期边界；本文只说明现有测试如何观察这些边界，不补充验收规则。

| 文件 | 已有证据与范围 |
|---|---|
| 001 | 原容量词汇扫描已撤下；真实依赖隔离待证。 |
| 002 | 实际纯选择、重锚投影及 Host compaction 设置判别。设置单元测试不证明已修改用户配置；相关配置副作用仍按既有待决处理。 |
| 003、012 | 实际 delta 切块与 TOML 渲染，独立 UTF-8 计量、解析和确定性核对。阈值用例包含渲染包装，不冒称原文字节就是最终输入大小。 |
| 004、005、007 | 终态文本判别、失败预算及 typed RequestKind 分派表。004 对“纯 XML”的歧义见 GAP-105；005 尚未贯通全部 attempt 结局。 |
| 006、008—011 | 实际纯候选、成功提交投影及请求种类；新物理 retry 身份仍待证。局部纯函数不替代真实 durable append/下一请求次序。 |
| 013 | 当前诊断 emit 的非致命行为。诊断失败或文本改变不影响真实决策仍待证；删除未进入产品的常量对象比较及在此重复的 Host fatal 展示。 |
| 014—017 | 同一会话 frame 材料、原子投影、完整 turn 边界及记忆呈现。017 的旧字段未进入接口，上游新 floor=cursor+1 又是测试自写算法；均撤下，真实恢复中的 Opening floor 与 ingest 起点留 TODO。 |
| 018 | 实际 mailbox/flight 的先后等待、取消、隔离、释放，以及普通新消息保真。废止的 ExplicitResume/continue 用例不恢复；跨多个 200 KiB 块追平及 durable producer 的完整续接仍 TODO。 |
| 019 | 辅助注入的 epoch 退休与 tentative horizon。 |
| 020 | 原 todowrite 永久保留随阶段窗口迁移；上游 retained 布尔透传不能证明真实选择，已撤下。未覆盖 raw 保留及有新快照载体才退休旧成功结果的实际连线仍 TODO。 |
| 021、022 | retry 请求种类和失败投影。正常、重试、squash、crash 四条真实入口共用 canonical 重建仍 TODO。 |
| 023 | typed park 返回及取消；事件驱动依赖与 durable producer 判据仍待完整验证。 |
| 024 | 实际 flight/准入竞争、stale callback 和已有修复场景。 |
| 025 | 已有停止准入用例；必需注入 fatal 和精确结算仍缺证，不再用测试自建 fatal 模型冒充产品。 |
| 026 | 实际 repair 注册与关闭真实 writer 后的 coordinator 失败；在途及随后观察者共享同一异常，原 flight 保留，零发送和成功通知。 |
| 027 | owner 构造性质及真实磁盘 blob 解码验证；冻结身份和 epoch 保留，损坏输入分类。真实 integration Fable consumer 已验证：同一真实声明闭包内，工厂调用、公开成员读取及请求种类匹配编译成功；外部直接构造 Main/Squash 分别在对应 consumer 位置报具体类型的访问拒绝。三项编译用例全部通过，源码与产物均隔离在临时目录；这些证据不替代旧 epoch 实际提交拒绝及全链保留 typed rejection 的缺证。 |
| 028 | 生产 K 窗口公式、同回合多提交及有地址边界；真实 XWire 在只有一条用户消息的工作循环中折叠已覆盖前导历史，同时保留 Opening 与窗口回合。注入材料端口不等于真实磁盘提交。 |
| 029 | 生产 selector 的窗口、coverage、请求边界、不可回退和 digest 证明；传入无窗口不证明上层只会在真实失败后选择紧急 Probe，仍需调用链证据。 |

018 原有纯 frame 覆盖集中在 010/011/015/016，flight 竞争在 024，handle 生命周期在 delegation 013/023。删除的源码名字检查、测试自建 Set 去重、空 frame fatal、claim/release 冒充 commit，并不算覆盖迁移；对应缺证留在 GAP-104 与 TODO。effect-accounting 004 的低层重复 receipt 也不能代替完整提交入口，见 GAP-105。

完成节点后先正式构建，再通过 `requirements/verification-system/tests/run.mjs` 与 `TESTS_MJS_FILES` 运行本目录及受影响所有者。TODO 不计通过，GAP-104/105记录未闭合状态；旧发布清单的DONE不作当前证据。未执行真实 provider 或人工验收。
