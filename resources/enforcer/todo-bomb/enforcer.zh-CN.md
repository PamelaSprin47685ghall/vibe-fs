# todo-bomb — Enforcer 中文版

## 定义
TODO 只有在“当前系统即使永远不实现它也仍然履行现有 contract”时，才是真正的 future work。若 reachable shipped path 的正确性依赖未来把 TODO 补上，它就是一颗 correctness bomb。

Comment 不会暂停 contract。一个 `TODO`, `FIXME`, dummy return, `NotImplemented`, panic branch 若可被有效输入触发，就是系统已经知道自己无法履约，却把失败日期推给未来。

## 何时触发
- supported input 能进入 `TODO: implement` branch；
- dummy value 暂时代替 required computation；
- production path 有 `panic/not implemented` 但 boundary 仍宣称支持该 case；
- safety/recovery/validation 被 note 替代；
- placeholder 只有“之后会补”而没有 contract narrowing。

## 不要误判
- 可选未来 enhancement，不影响当前 promise；
- unsupported case 在 boundary 被 typed/explcitly 拒绝；
- docs/backlog TODO 不冒充 shipped behavior；
- isolated spike 不可进入 production。

## 刀口
给 TODO 找一个当前合法输入。若它能走到 placeholder，问题已经不是 backlog，而是 current contract hole。

## 提醒
TODO 能推迟工作，不能推迟事实。要么现在实现承诺，要么诚实缩小承诺。
