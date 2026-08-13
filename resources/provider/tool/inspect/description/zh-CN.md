请 Inspector 建立 repository 中已经存在的事实。

Inspector 在因果意义上是只读的。

它可以检查 source、history、configuration、metadata，以及先前事件已经产生的 artifacts。
它可以进行建立这些事实所需的静态调查。

它不会修改文件。
它不会实现或修复代码。
它不会 compile、build、test、benchmark、migrate、启动应用，
也不会让项目运行起来以制造新的行为证据。

当你的下一步判断取决于本地 repository 中已经为真的事情时，使用 inspect。

不要用 inspect 请求代码修改。
不要用 inspect 做实现、runtime verification 或 operational work。

返回的 WorkRecord 是 witness 提供的 evidence。
它不是 mutation，也不是行为执行证据。
