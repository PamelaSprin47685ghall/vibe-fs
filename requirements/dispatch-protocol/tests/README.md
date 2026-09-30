# dispatch-protocol 测试

规则以 WHAT 为准。本目录区分调度事实、Host 证据、准入交接与执行结果，不把“函数存在”当作没有旁路的证明。

- 001 实际 owner-root/continuation 在 Host 端观察注册好的 claim；全体合成消息来源仍需逐一验证。
- 002/005/006 有真实生产 projection、sequence、hash 输入与兼容解码。005 单独改变六个组成要素；它以注入 hash 观察确定性输入，未声称穷尽哈希碰撞或跨进程故障。
- 003 实际 receipt 发送、关闭并重开 journal，确认两种外观的 receipt 均不授予权限。004/007/008 的历史用例多为同进程额外 journal writer，不是 OS crash。保留实际拒绝、未知、查不到证据、不可读及并发 flight 的行为断言。
- 009 把 Host Promise 保持未决，直接观察 Detached 是否返回，再释放端口；不再用 120ms 竞争当完成事实。晚到 verdict 的用例注入于 dispatcher 端口，不证明 SDK/HTTP 适配器自身正确分类。构造器字面名单已删除。
- 010/011 使用实际接受的 root profile 续行，验证发送参数、完整 PromptKey、Origin 与 LogicalRun 元数据。profile 没有 model 字段是投影证据，不代替编译边界或全部调用者证明。
- 010 的 managed assignment 回归在 journal 重开后继续 Manager 与 Blogger 子会话，确认 run/root/participant 不变，副本的精确工具权限不丢失，且 continuation 不重新取得 root identity seed。
- 012 实际 claim acceptance 与独立 turn reconciliation，完整 handoff/容量所有权仍有 TODO。013 是 journal 重开与显式恢复，不能证明真实插件构造无副作用。014 的自造 fatal descriptor 已撤，真实 invariant incident 仍需证明。
- 015 的载体属性测试、诱饵/getter/冲突输入继续保留；新增实际插件的非法 PromptKey 和非法 agent 反例，防止错误被折叠成 Missing 后获得新 authority。

`support/authority.mjs` 仅准备实际 Persona、Journal 与 dispatch 入口所需的测试输入，并负责临时目录和 journal 重开；不在夹具中复制权限或恢复算法。

GAP-136 记录证明缺口，GAP-137 保留 [009] 晚到 Fatal 的合同分岔；GAP-138 记录真实非法 carrier 升权缺陷及修复。测试里注入的低层观察不是新的验收规则。

正式执行先构建，再通过 verification-system/tests/run.mjs 的 TESTS_MJS_FILES 选择本目录及受影响的 authority、chat admission 测试。TODO 和跳过不算成功。实际插件用例依赖隔离的 fixture 配置，不修改开发者 OpenCode 配置；目前不使用真实 provider，不宣称 Long Stroke 完成。
