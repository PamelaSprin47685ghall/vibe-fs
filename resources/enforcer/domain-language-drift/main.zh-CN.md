# domain-language-drift — Main 中文版

## 现在该做什么
在 owning context 选择 canonical terms，把 code/types/events/tests/docs 一起收敛；overloaded term 先拆概念，再 rename。真正跨 bounded context 的差异保留，并在 border 显式翻译。

## 为什么这很重要
稳定 vocabulary 是认知压缩：一个词学一次，处处复用。同义词泛滥与一词多义会把这份压缩反向展开，每次阅读都重新 disambiguate。

## 常见假修复
- 建 glossary 为长期不一致辩护。
- 发明一个“中性第三词”统一两个其实不同的 contexts。
- 只 rename type，不改 tests/events/docs/provider surface。
- 为 compatibility 保留旧 synonym，却没有真实 external creditor。

## 验证
搜索 retired synonyms 与 overloaded term。一个 domain expert 与一个 code reader 在同一 context 看到 canonical term 时，应指向同一个 concept。

## 完成条件
每个 context 内词与概念稳定对应；语言差异只存在于真实 boundary，并由显式 translation 承担。
