# domain-language-drift — Enforcer 中文版

## 定义
Domain language drift 发生在词与概念不再一一对应：一个概念被叫成多个名字，或一个名字被拉去承载多个概念。

名字是 code、test、doc 与人类讨论之间的 join key。Vocabulary 漂移后，同一个词不能稳定定位同一个概念，讨论开始出现“我们说的是不是同一个 X”的额外解码。

## 何时触发
- 同一 context 内 `amount/total/prcAmt` 实际指同一事实；
- 一个 `status` 在不同模块分别表示 lifecycle、transport、approval；
- event/type/test/doc 对同一概念使用不同词；
- rename 只改了一层，旧 synonym 继续活在 provider 或历史无关的现行 surface。

## 不要误判
- 不同 bounded contexts 可以有不同 ubiquitous language，只要 border 显式翻译；
- 单个名字本身说谎属于 `misleading-name`；
- adapter 中技术协议词翻译成 domain term 是正常边界；
- 标准协议词不是本地 vocabulary drift。

## 刀口
在一个 context 内做两向检查：**一个 term 是否只指一个 concept？一个 concept 是否只有一个 canonical term？**

任一方向失败，语言已不再可靠承载 identity。

## 提醒
Vocabulary 不是文案层。它是模型的一部分；词与概念失去一一对应，代码就失去稳定的语义索引。
