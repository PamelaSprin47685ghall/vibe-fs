# dirty-hack — Main 中文版

## 现在该做什么
追到 hack 所绕过的 invariant/owner，修 canonical model，使这个 case 能通过正常路径表达；然后删除 bypass。若它其实是真实 domain exception，就给它正式名字、类型、测试与 authority，不再叫 workaround。

## 为什么这很重要
Hack 的危险在扩散：下一位维护者看到它，会以为“这里就是特殊”，再为邻近 case 加第二个例外。最终真实规则散在一串 `if special` 中，官方 abstraction 只剩宣传价值。

## 常见假修复
- 给 hack 加更详细 comment。
- 把 magic branch 移进 `compatibility`/`utils` 隐藏。
- 再加一层 facade，让 caller 看不见 bypass。
- 为了尽快绿，复制一条 alternate path。

## 验证
删除 special case 后，目标 scenario 应仍通过 canonical invariant/model；若删不得，就说明根修复尚未完成或它其实应被正式建模为 domain exception。

## 完成条件
系统只有一套可解释的模型；没有局部代码在暗中承认 canonical invariant 其实不成立。
