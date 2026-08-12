# exception-driven-control-flow — Main 中文版

## 现在该做什么
把预期 alternatives 改成 Option/Result/iterator/typed retry outcome 或普通 branching；只把“ordinary contract 无法继续”的情况留给 exception。Foreign throwing API 在第一层 owned adapter 消毒一次。

## 为什么这很重要
正常路径若依赖 exception，真正 control graph 不在 call site。reader 既看不到所有 outcomes，compiler 也无法迫使 caller 处理它们；catch-all 更容易把完全不同的故障混在一起。

## 常见假修复
- catch 后返回 magic null/string。
- 每层都包 try/catch，让 exception 更“local”。
- boolean + global lastError。
- 保留 throw，只加 comment 说“这是正常 not-found”。

## 验证
所有 ordinary outcomes 应从函数类型或紧邻语法可见；happy path 不再要求先抛异常才能继续。

## 完成条件
预期分支局部、显式、typed；exception 重新只承担真正打断普通 contract 的故障。
