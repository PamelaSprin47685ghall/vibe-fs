# WHY：为什么测试世界必须与 Fable 世界隔离

## 不可替代的存在理由

「测试能摸到实现内部」是语义证明的第一杀手。它让测试在问「F# 是怎么实现的？」而不是
「这个 semantic component 承诺什么？」，于是：

```text
内部 helper 改名        → 测试崩（与产品承诺无关）
Map 换 Dictionary      → 测试崩（与产品行为无关）
DU 增一个 case         → 测试崩（与业务边界无关）
Fable 版本升级         → 测试崩（与系统语义无关）
```

每一件都让「绿」变成对偶然实现的供奉。历史教训（G4R 时代 31 个 E2E flake、mangled method
discovery、`SessionQuiescenceGate` 测试直接扫描 emitted names）是同一条病：测试通过
**获得不该有的权力**而变脆。

`js-semantic-surface` 存在的理由：**把「测试与实现之间的边界」本身变成被证明的规则。**

## 为什么是元合同（META）而不是产品包

产品包拥有领域事实：`managed-session-lifecycle` 拥有 session 生命周期，`delegation` 拥有
fork/join 语义。它们各自有「这条产品规则怎么证明」的义务。

本包不拥有任何领域断言，它拥有的是**语义测试边界的通用规则**：什么算正式 surface、
什么数据形状可以穿过边界、什么权力测试永远拿不到。同一个规则服务于所有产品包——「任意
产品 law 都值得拥有 JS-native surface」这种共享事实只有 META 包能拥有。

## 为什么不能并入 verification-system

`verification-system` 回答「怎么证明」（证据分层、可红性、fail-closed、时间确定性）。
`js-semantic-surface` 回答「测试世界的边界是什么」（surface 是什么、JS-native 是什么、
Fable representation 为什么不是 contract）。独立变化测试：把 `domain.mjs` 换成新的
JS surface 全家——本包 HOW 变，verification-system 的 ladder/gate 机制不变；把 gate 从
regex 扫描换成 AST 扫描——verification-system HOW 变，本包宪法不变。

合在一起，一次改动会同时牵动两种失败意义：「这个证明可不可信」与「这个测试有没有越界」。

## RED 是什么样

```text
语义测试 deep-import dist 内部模块
语义测试读 .tag/.fields/.cases()
语义测试构造 FSharpMap/FSharpList 作输入
语义测试依赖 mangled emitted names
新增 surface 但无 contract test pin 名字
测试需要访问 → 生产 export internal
```

## 考古：本包的条款来源

- 六条宪法直接来自 Operation Clean Slate（TASK.md P0 章节）「先冻结宪法」。
- SURFACE-001..006 历史编号（provider-language / provider-projection / finality /
  participant-horizon / verification-system 交叉引用）在本包收编为正式条款，消除悬空引用。
- JS-001..020（repository-programming HOW 的历史 js-tools 编号）不归本包；它们描述
  capability 面，不是测试边界。
