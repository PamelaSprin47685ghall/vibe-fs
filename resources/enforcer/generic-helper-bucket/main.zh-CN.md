# generic-helper-bucket — Main

## 现在该做什么
把 junk drawer 清空：每个 operation 都交回给赋予它 meaning 的 concept、boundary 或 owner。

不要把一个 `utils` 拆成五个更小的 `utils`。修的是 ownership，不是 sharding。

## 为什么重要
Generic bucket 会悄悄变成 dependency hub，因为所有人都被允许依赖它，而它也被允许装几乎任何东西。

这让 implementation 当下很方便，evolution 时却异常昂贵。一个 domain-neutral formatting helper 旁边放 database helper；很快所谓“shared” module 开始 import infrastructure，再 import domain types、configuration。此后 lower layer 无法干净依赖它，higher layers 又全依赖它；想抽走其中一个 piece 时，才发现 dependency graph 从未被任何人有意设计过。

它还有组织层面的成本：bucket 的存在会训练 contributor **不做 ownership decision**。每个 rushed change 都获得一个默认目的地。

## 修复策略
先分类 contents，再移动：

- domain rule → 放回表达其 invariant 的 domain/type/module；
- boundary translation → 放到拥有该 boundary 的 adapter/codec；
- infrastructure operation → 放在它控制的 resource/effect 附近；
- 真正广义、纯 technical primitive → 给 narrow technical name 与明确 dependency policy；
- one-owner implementation detail → 保持 local/private；
- 文本重复但没有 shared meaning → 宁可局部 duplicate，也不要发明 false common ownership。

移动以后修 imports，让 dependency direction 跟新 ownership 一致。只搬 function，却留下反向 dependency 指回旧 bucket，不算完成。

优先 local helper，直到 independent consumers 真正证明存在 stable shared concept。Extraction 应当跟随 semantic reuse，而不是预判它。

## 决策分支
- **Helper 编码 domain policy：**即使多个 caller 使用，也回到 domain owner。
- **Helper 是 boundary codec/parser：**与 boundary contract/test colocate。
- **Helper 真正 generic + pure：**保留/抽出，但使用 precise technical name，并禁止 higher-level dependency 泄漏进去。
- **两个 domain code 相似但 reason 不同：**局部 duplicate；相似性不是 ownership。
- **Bucket 实际 coherent，只是名字差：**rename 到真实 concept，别 gratuitous movement。
- **移动 helper 会造 cycle：**cycle 说明 ownership/dependency direction 需要重设计；不要用 `utils` 当 cycle escape hatch。

## 常见假修复
- 把 `utils` 改名 `shared` / `common-core`，内容完全不动。
- 按机械 category 拆 `stringUtils/objectUtils/miscUtils`，里面仍装 unrelated domain rules。
- 造一个全局 `platform` package，让它成为更有地位的新 junk drawer。
- 看见第二次 textual duplication 就抽 shared helper，却没问两个 site 是否依赖同一个 semantic law。
- Helpers 已搬走，却永久从 old bucket re-export，两个 dependency path 都继续活。
- 文件头写 ownership comment，但仍继续接受 unrelated functions。

## 验证
Redistribution 后，每个 module 都应有 exclusion rule：maintainer 不只说得出“什么属于这里”，还说得出“什么不属于这里”。

检查 dependency direction。Lower-level technical module 不应仅因为旧 helper 需要，就 import high-level domain/infrastructure。

搜索 old bucket，删除 obsolete re-export/import。Generic hub 很容易复发时，加 architecture boundary 防回流。

Invariant：

> Shared code 之所以 shared，是因为它拥有 shared semantics，而不是因为它没有家。

## 完成条件
Repository 不再有 ownerless code 的默认抽屉。

每个新 helper 都先迫使作者回答“谁拥有它”，而不是让仓库自动回答“放 common”。
