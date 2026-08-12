这是当前 agent 的可编程文件系统工具。

下面的基类由本次请求中实际可用的 capability 生成。方法出现即可使用；方法缺席表示该 capability 不可用。

必须恰好定义一个名为 Js 的 class，继承 JsProgram，并实现 async run()。
