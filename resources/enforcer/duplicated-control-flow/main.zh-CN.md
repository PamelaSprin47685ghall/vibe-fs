# duplicated-control-flow — Main 中文版

## 现在该做什么
先确定协议真正的 owner，再把顺序、失败、retry、取消和 side-effect 约束集中到这一处。其它入口只负责翻译输入、调用 canonical workflow、翻译结果。

## 为什么这很重要
重复 control flow 会把一个 protocol 变成多个版本。最危险的不是明显分叉，而是“几乎一样”：99% 相同让人以为它们等价，剩下 1% 的错误处理或 ordering 差异恰好只在故障时暴露。

这类 bug 很难通过 code review 发现，因为每个局部实现单看都合理；错误存在于“两个 owner 本应相同却已经不同”。

## 修复策略
1. 写出 canonical workflow 的 ordered facts；
2. 区分 domain decision 与 adapter effect；
3. 选一个 owner 承担 protocol；
4. 把多个入口改成 caller，而不是复制者；
5. 把 legitimate variation 做成显式参数或不同 protocol，不要藏在分叉副本里；
6. 删除旧实现，避免“先留着保险”。

## 分支判断
- 若两处必须因同一规则同时变化，统一 owner。
- 若两处只有表面相似、失败语义不同，保持分离，不做 DRY 表演。
- 若差异是合法 policy variation，把 variation 建模，而不是维护两个 copied workflow。
- 若历史 recovery 需要旧步骤，只保留 decode/replay compatibility，不保留旧 write authority。

## 常见假修复
- 抽几个 helper，但两套顶层 sequence 仍各自决定 ordering。
- 复制一份 workflow 再“以后同步”。
- 用 comments 写“保持与 X 一致”。
- 创建一个 generic orchestrator，但真实 branching 仍散在 callers。
- 只做文本 dedup，把不同语义硬塞进一个 boolean-heavy function。

## 验证
改变一个关键协议规则，例如“persist 必须先于 publish”或“charge failure 必须 release reservation”。production 中应只有一个 owner 需要改，所有入口自然继承该规则。

## 完成条件
协议知识只有一个可修改权威；入口可以多，representation 可以多，但 workflow 的 ordering / failure / retry semantics 不再由多个实现各自解释。
