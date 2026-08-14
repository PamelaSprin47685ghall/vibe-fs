# `provider-projection`

**WHY**  
多个 feature 若直接修改 provider message list，顺序、冲突、semantic equality、wire identity 与 prefix 都会退化成装配偶然性。

**OWNS**
- immutable semantic projection input。
- typed projection intent model。
- canonical planning/order、显式 merge 与 conflict；不得靠 registration order 选边。
- deterministic renderer。
- Semantic 与 Wire representation 分型；semantic equality ≠ wire equality。
- typed state → representation 单向关系；representation 不反向创造 authority/state/lifecycle。
- layout/escaping mechanism 只拥有 representation，不拥有 prose meaning。

**DOES NOT OWN**
- Repair/Review/Todo/Companion/Strength intent 是否应该存在。
- horizon admission、language choice、prefix epoch commitment。
- lifecycle/provider execution。
- 当前 intent case 列表永久同构现有代码。

**DEPENDS ON**
- `participant-horizon`
- `provider-language`

**PROVIDES**
- `semantic intent → deterministic provider representation`。

**FAILURE MEANING**  
RED = 同样 semantic intent 集因装配顺序得到不同 provider 世界，冲突静默选边，或 representation 被反解析成 authority/state。

**INDEPENDENT CHANGE**  
替换 TOML/wire renderer 或 planner，只要 semantic intent、horizon 与 equality contract 不变。

**CURRENT EVIDENCE**  
`docs/{why,what,shape,how,proof}/projection.md`；type `Domain/{ProjectionIntent,ProjectionPlanner,ProjectionRenderer,ProviderProjection,SyntheticToml,XPrefixProjection}.fs`；projection algebra tests；Synthetic TOML 作为表示机制证据。
