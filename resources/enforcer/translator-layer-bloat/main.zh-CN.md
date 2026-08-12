# translator-layer-bloat — Main

先做一次“删除实验”。把中间层拿掉，直接连接两边；逐项列出会失去什么。如果没有 invariant、translation、authority narrowing、failure isolation、lifetime ownership 被破坏，就删掉这层。

如果它确实有一项真实职责，就把职责写得可见，并按它命名。`ProviderCodec`、`AuthorizationBoundary`、`TransactionScope` 比 `FooManager`、`CommonAdapter` 更能说明为什么 caller 必须经过这里。

常见 repair：

- 纯 forwarding method 直接 collapse；
- 1:1 DTO mirror 删除，保留真正 semantic translation；
- real behavior 搬到拥有该 invariant 的 boundary；
- surviving adapter 只暴露经过转换后的类型，而不是两边 private model 同时泄漏；
- generic orchestration noun 改成它真正保护的 contract 名称。

常见假修复：

- 给 forwarding layer 加 interface，让空 hop 更“架构化”；
- `Manager` 改名 `Facade`，行为不变；
- 用 code generation 生成更多 forwarding boilerplate；
- 把原来一层 pass-through 拆成 request mapper + service + response mapper 三层，但所有类型仍 1:1；
- 为证明 layer 有价值，硬塞 logging/metrics；observability 不会自动创造 semantic boundary；
- 删除 layer 后把其 pass-through helper 全塞进 `utils`，只换一种 ownership 模糊。

验证 surviving layer 时，应该能构造一个“不经过它就会出错”的 boundary case：invalid wire 被拒绝、foreign ID 被转换、permission 被收紧、transaction 被正确 scoped、provider error 被映成 typed outcome。如果没有任何这种 case，存在理由仍然可疑。

反过来，删除空层后做 semantics-preserving tests：caller contract 应不变，stack/dependency graph 更短，mock/fixture 数减少。

不要追求最少层数。一个系统可以有很多真实 boundary；问题只在**穿过某层什么都没发生**。有价值的 architecture 往往不是层少，而是每层都能回答自己为什么值得被跨越。

完成时，每个 surviving hop 都改变至少一种东西：knowledge、representation、authority、lifecycle 或 failure law。否则就让调用直接到真正 owner。

> Abstraction 是压缩知识，不是延长调用路线。