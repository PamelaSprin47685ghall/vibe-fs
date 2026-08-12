# implicit-convention-magic — Enforcer 中文版

## 定义
Convention 变成 magic，不是因为“约定优于配置”这句话本身错，而是 correctness 被编码进 filename、path、annotation、reflection、discovery order 等 ambient ritual，call site 与 type system 都看不见它。

隐藏 convention 本质上是一个**以缺席为语法的 API**：你没有显式调用某东西，只是把文件放对地方、名字拼对、annotation 写对，然后期待 runtime 自动知道。漏掉 ritual 时往往不是编译失败，而是什么都没发生。

## 何时触发
- handler 是否参与由文件名后缀决定；
- route/plugin/job 靠 directory scan/reflection 自动发现；
- annotation 拼错后 runtime 静默不注册；
- registration order/placement 承担 correctness，但没有 typed declaration；
- 新人只能靠“这里一直这么放”学到参与规则。

## 不要误判
- convention 只是 ergonomic sugar，背后有明确 registry/build check；
- directory layout 只服务人类导航，不驱动行为；
- compiler/codegen 能机械验证完整性与 uniqueness；
- open plugin ecosystem 的 dynamic discovery 如果就是产品 capability，可以保留，但 contract/validation 必须明确。

## 刀口
故意重命名、移动或漏掉一个 participant。**系统是在 build/startup 明确告诉你 contract 被破坏，还是静默改变行为？**

后者就是 magic 在承担 correctness。

## 与近邻区分
`implicit-control-flow` 隐藏“什么时候运行”；这里隐藏“谁会参与”。

`missing-architecture-gate` 可能是 remedy：若 convention 必须存在，机械 completeness check 可让它从 folklore 升格为 contract。

## 例子
- 正例：只有文件名以 `.handler.ts` 结尾才被扫描，拼错后 route 默默消失。
- 近邻：文件可随意摆放，但 explicit typed registry 列出所有 handlers，startup 校验重复/缺失。
- 反例：annotation 由编译器 macro 检查并生成显式注册表，漏注解直接构建失败。

## 提醒
Convention 可以省字，不能省证据。若一个 ritual 决定 correctness，就必须有能失败的机械 owner。
