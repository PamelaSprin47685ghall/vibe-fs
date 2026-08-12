# implicit-convention-magic — Main 中文版

## 现在该做什么
把 correctness-critical participation 变成显式 typed registration、generated manifest 或 build/startup completeness contract。Convention 可以留下当 sugar，但 authoritative model 必须可见、可检查、可失败。

## 为什么这很重要
Convention 用更少语法换取更多组织记忆。规模小时很舒服；规模大后，系统行为取决于一套源码本身不声明的民间仪式。最坏的 failure mode 不是报错，而是**缺席**：某个 handler、migration、test、hook 根本没运行，却没有任何红灯。

## 修复策略
- 找出 convention 实际编码的 relationship；
- 把 relationship 建成 data/type/manifest；
- one owner 校验 completeness、uniqueness、compatibility；
- 让 filename/path/annotation 仅作为生成显式 model 的输入；
- open-world discovery 需要清晰版本/validation/failure semantics。

## 常见假修复
- 再写一页文档解释命名规则。
- 加 comment “do not rename”。
- 扫描更多目录，让 magic “更智能”。
- 用 runtime warning 代替 startup failure，即使缺 participant 会破坏 correctness。
- 把 registry 藏进另一个 reflection layer，仍然无法静态/启动时验证。

## 验证
对 fixture 做 rename/move/omit/duplicate：需要参与的对象一旦不满足 contract，构建或启动应明确失败；纯导航上的移动不应改变行为。

## 完成条件
参与关系不再依赖团队记忆；源码或生成物能明确回答“谁注册了、为何有效、违反时哪里失败”。
