# implicit-convention-magic — Main

把 correctness-critical convention 降级成 sugar，把 explicit checked model 升成 authority。

先找出 convention 实际编码的关系：谁参与、名字是什么、实现哪个 contract、属于哪类 capability、需要哪些 dependencies。把它做成 typed registry、manifest/schema、code declaration 或 build/startup completeness check。

Convention 仍可保留用于减少 boilerplate，但它必须**编译/生成/验证到显式 model**，而不是 runtime 唯一真源。

常见假修复：

- 写更多 README 告诉大家 filename 规则；
- scanner 扫更多目录，让“忘放哪里”概率低一点；
- violation 时打 warning 但继续启动，feature 仍然静默缺失；
- 用 annotation 替 filename，却仍没有 completeness owner；
- 建一份 registry，但 runtime 继续直接 discovery，registry 只是文档；
- 给 magic convention 加 test fixture，却 production 新 participant 仍可能绕过 test。

验证要主动 rename/move/omit participant。系统应在 build/startup 的明确 boundary fail，并告诉你哪个 contract 不完整，而不是等某个用户发现 route/tool/handler 没出现。

还要测 stale registration：explicit model 声称 participant 存在，但 implementation 被删/改 signature，应机械失败。这样 authority 才真正在 model，而不是又变成一份会漂的名单。

如果 framework 强制 convention-over-configuration，至少在 adapter/startup 建一个 completeness projection，把 framework discovery 结果变成可验证事实，并 fail closed。无法改变 framework 不等于必须把它的隐式规则泄漏到整个 application。

不要把所有 convention 都变成重型 manifest。若编译器/type system 已能直接检查关系，最简单的 explicit declaration 就够了。目标是让 omission 变 visible，不是制造配置文件数量。

完成时，参与关系能被代码/构建工具明确列出和验证；文件名/路径/annotation 最多是方便写法，不再是唯一保存 architecture 的地方。

> 好 convention 让正确事情更省字；坏 convention 让错误事情连错误都不报。