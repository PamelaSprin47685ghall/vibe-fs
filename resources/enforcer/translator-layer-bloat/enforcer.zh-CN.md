# translator-layer-bloat — Enforcer

Translator-layer bloat 的问题，是一个 broker/manager/coordinator/adapter/facade 让调用多走一跳，却**没有改变 knowledge、representation、authority、lifecycle 或 failure contract**。

一层 abstraction 值得存在，是因为跨过去以后有些事情真的不同：external ID 被映成 domain ID、wire error 变 typed error、authorization 被收紧、transaction/lifetime 被拥有、batching/backpressure 被建立、bounded context 被隔离。

如果所谓 layer 只是：

```text
A.doX(dto) → B.doX(dto)
A.getY(id) → B.getY(id)
```

同样 DTO、同样语义、同样失败、没有 invariant，那么它只增加了一个名字、文件、stack frame、test double 和搜索位置。Indirection 没有 information hiding，就是距离。

以下情形触发：

- `Manager`/`Coordinator` 每个 method 逐字转发给 `Service`；
- 两套 DTO 字段 1:1 copy，连 semantic name 都没变；
- adapter 不做 validation/normalization/error mapping，只重新包装 call；
- layer 唯一理由是“architecture 应该有 service/repository/facade”；
- 每次真实 change 都必须穿过 layer，但 layer 从不作任何独立决定；
- test/mock 数量因为空 hop 增加，却没有新增可证明 contract。

不要误杀真正薄 adapter。Boundary 可以很薄，只要它拥有真实差异，例如 protocol translation、anti-corruption、auth、resource lifetime、retry/failure isolation。价值不按行数衡量。

与 `facade-hides-mess` 区分：facade 可能确实给 caller 一个干净 surface，但下面 ownership 仍烂；translator bloat 则是中间 hop 自己没有 semantic job。与 `framework-tax` 区分：后者是框架 ceremony 主导 architecture；本规则可以完全没有 framework。

诊断问题：**把这一层删掉，让两边直接相连，会丢失哪条 invariant 或哪种知识变化？** 如果答不出，layer 没有赚回它的认知税。

> Layer 不是因为“层次感”存在。每一次跨层，都应该让某种知识、权力或 failure semantics 发生真实变化。