# `provider-language`

**WHY**  
同一 participant session 中途切换自然语言，会让世界观、工具后果与历史形成两个认知环境；机器 protocol identifiers 则必须保持同一 identity。

**OWNS**
- provider natural-language universe 的语言类型。
- language 在 session 创建时绑定并保持不变。
- child/attached/internal execution 继承 owner/commissioner language，不各自重读全局偏好。
- 全局偏好变化只影响未来 session。
- localizable prose 与 invariant protocol identifiers 分类。
- bound session 缺 localization 时拒绝继续生成不一致语言文本。
- meaning 属 semantic owner；language 属 session；rendering 属 machinery。

**DOES NOT OWN**
- prose 的业务意义、Persona、Role/Tool/Runtime contract。
- provider wire layout。
- 当前只支持 EN/zh-CN 是否永久。

**DEPENDS ON**
- `session-ontology`

**PROVIDES**
- participant-visible prose 的语言一致性 guarantee。

**FAILURE MEANING**  
RED = 同一 session 出现多个自然语言世界、child 与 owner 语言漂移，或翻译改变 tool/wire identity。

**INDEPENDENT CHANGE**  
新增 locale 或改变 locale resource layout，而 identity/horizon/projection 语义不动。

**CURRENT EVIDENCE**  
PROMPT-017/019；HOST-026；type `Domain/ProviderLanguage.fs`、wiring `Infrastructure/Resources/{ProviderResources,ProviderProse}.fs`；`scripts/checks/language-parity-gate.mjs`；PromptRestoration。
