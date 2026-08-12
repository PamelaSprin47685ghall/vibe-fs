# cross-layer-internal-import — Main

把 dependency 拉回 owner 正式发布的 contract。

先别急着“换 import path”。问 foreign layer 真正需要的是什么：query、command、stable value、event、capability、adapter result，还是只为了方便拿了一个 internal helper？让 owner 发布**最小 semantic surface**，然后删掉对 private representation 的依赖。

常见 repair：

- internal storage row → owner query/result type；
- generated Fable/runtime internals → 稳定 facade/export；
- private helper → 要么搬到真正 owner，要么把需要的能力正式做成 public contract；
- SDK/provider object shape → adapter decode 成 application/domain type；
- foreign union case inspection → owner 返回 caller 真正需要的 closed outcome；
- production import test support → 把共同逻辑移到真正 production owner，test support 只消费它。

不要通过 re-export 私有成员来“修复”。如果 `PublicFacade` 只是 `export * from ../internal/*`，foreign knowledge 一点没减少，只是 import path 漂亮了。

也不要为了消除一条 internal import 造一层 forwarding-only translator；如果新层不改变 knowledge/representation/authority，会落入 `translator-layer-bloat`。

常见假修复：

- 给 internal symbol 加 `public` / export，就说 contract 已建立；
- 复制 private type 到 consumer，然后两边靠手工同步 shape；
- 关闭 architecture lint，让 import “合法”；
- 通过 reflection/dynamic import 绕过静态 dependency，概念上的越界仍然存在；
- test facade 直接暴露 compiler-emitted internals，导致 compiler upgrade 变成业务 breaking change；
- 把所有 shared internals 塞进 `common` package，原 ownership 问题只是换了地址。

验证应做 owner-side refactor：保持 declared contract 完全不变，重命名 internal module、换 storage representation、升级 compiler-generated shape、移动 helper。Foreign layer 应继续编译/运行。

再做 dependency scan：跨 layer 的 imports 只应指向正式 supported entry，禁止 deep path/private generated detail。机械 gate 可以保护这条**代码边界**，因为它检查的是 executable dependency，不是 free-form prose。

如果 foreign layer 真正需要某个 internal fact 才能正确工作，别把它叫 internal；重新设计 contract 并明确 owner。这比长期偷偷 import 更诚实。

完成时，source dependency graph 与 architecture 声称的 boundary 一致；private change 不再通过 hidden import 产生远距离破坏。

> 内部细节一旦有人必须依赖，就已经不是“细节”；要么收回依赖，要么正式承担 contract 责任。