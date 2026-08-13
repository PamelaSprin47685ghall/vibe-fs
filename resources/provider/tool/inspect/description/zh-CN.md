请 Inspector 建立 repository 中已经存在的事实。

Inspector 在因果意义上是只读的：
它可以读取、搜索并进行静态调查，
但不会修改文件、实现修复，也不会让项目运行起来以制造新的行为证据。

当你的下一步判断取决于一个已经存在的 repository 事实时，使用 inspect。

不要用 inspect 请求代码修改、实现、修复、测试执行、build、benchmark、
migration，或任何会改变世界的工作。

返回的 WorkRecord 是 witness 提供的 evidence，不是 mutation。
