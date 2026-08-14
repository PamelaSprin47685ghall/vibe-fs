# Context continuity

## `semantic-trace`

WHY: participant life 中有些语义材料必须长期可定位、可重建；若只依赖当前 transcript head 或临时摘要，后续 review/context/recovery 会把“现在看见什么”误当“当时发生什么”。

OWNS:
- append-only semantic history 的 canonical facts。
- Opening/assistant reasoning/tool evidence 等需要长期保留材料的 typed capture boundary。
- semantic parts 与 transport/wire identity 分离。
- stable frontier/range/cutoff，可冻结某次 request 所见证据边界。
- bounded WorkRecord 的 source material provenance；工作记录是从 trace 物化，不是第二事实源。
- capture 不应把未发生的 speculative/provider-local state提前写成历史。

DOES NOT OWN:
- context compression policy。
- provider projection/wire shape。
- review judgement。
- Blog/Companion 的具体 memory representation。
- 当前 XTrace type/module/file layout。
- participant horizon admission；trace 位于 horizon 之前，只有后续 projection/record delivery 才需要该保证。

DEPENDS ON:
- `durable-events`（durable parts）。

PROVIDES: 可冻结、可追溯、可供 review/context/recovery 消费的 semantic history。

FAILURE MEANING: RED = 后续系统无法证明一段工作记录对应哪个真实历史 frontier，或临时/未发生材料可以污染 canonical history。

INDEPENDENT CHANGE: 把 XTrace 存储/part 编码整体重写，只要 semantic capture、frontier 与 provenance contract 不变。

CURRENT EVIDENCE: XTrace capture/materialization；COMPANION/LWR；TODO/REVIEW request-range bounded records；Strength unpromoted≠history。

---

## `context-compression`

WHY: provider history 可能超过可用上下文；压缩若依赖预测窗口或把候选先提交再回滚，会把模型容量猜测与未发生世界写进产品事实。

OWNS:
- context pressure 的 recovery policy，优先由真实失败/明确容量事件驱动，不把模型窗口估算当产品真相。
- candidate compressed prefix/memory 在成功提交前不是历史事实。
- compression 只替代可证明 covered 的历史区域；Opening/其它受保护材料有明确 floor。
- semantic memory 与 raw gap/physical tail 的边界。
- compression result 的 bounded input/output contract。
- failure 不靠 provider error prose 分类。

DOES NOT OWN:
- semantic trace source facts。
- prefix byte-stability law。
- provider renderer。
- Companion/Blogger 的具体 summarization Persona。
- 当前 200 KiB 等实现常数是否永久；只有被证明是产品合同的上界才进入未来 WHAT。

DEPENDS ON:
- `semantic-trace`
- `provider-projection`

PROVIDES: “何时/哪些历史有资格被语义替换”的 compression guarantee。

FAILURE MEANING: RED = 未提交候选污染真实历史、压缩覆盖不完整证据，或系统靠猜模型窗口主动改写用户世界。

INDEPENDENT CHANGE: 从当前 failure-driven X/Y pipeline 换成另一 semantic compressor，而 trace/prefix laws 不变。

CURRENT EVIDENCE: `docs/why/context.md`；CTX failure-driven recovery、PrefixProbe、ActivePrefixEpoch；Companion/Y projection。

---

## `prefix-stability`

WHY: provider cache 与认知连续性依赖“已呈现的过去不会无故重排”；若同一 semantic epoch 的历史字节不断搬家，identity/language/guidance 即使语义相同也会形成新的世界。

OWNS:
- append-only provider prefix law：同 epoch 后续 request 的已提交前缀保持 byte-stable，除非明确发生合法 cold boundary/rebase。
- prefix epoch/cold-boundary 的语义。
- committed prefix 与 desired candidate 的分离；candidate 不等于 committed。
- provider/model/system/tool/history 中参与 prefix identity 的范围。
- reanchor/rebase 一旦合法提交，不因后续 provider failure 回滚。
- historical synthetic material 不应重定位；找不到原 anchor 时宁可不 replay 也不猜新位置。

DOES NOT OWN:
- 为什么需要 compression/rebase。
- provider language/identity/cognition 内容；只要求它们在同 life 内若属于 prefix identity则稳定。
- renderer implementation。
- 当前 gap-anchor / fake-tool / Cursor suffix HOW。

DEPENDS ON:
- `provider-projection`
- `context-compression`
- `provider-language`
- `participant-identity`

PROVIDES: 同一 semantic history 在 provider 边界上的连续性 guarantee。

FAILURE MEANING: RED = 无业务语义变化时历史被重排/改字节，或未提交 candidate 被当成 stable prefix。

INDEPENDENT CHANGE: 完全替换当前 HOST-013 gap anchoring/wire representation，只要 append-only prefix law 与合法 cold-boundary semantics 不变。

CURRENT EVIDENCE: HOST-013 prefix law、`ProviderProjection.isAppendOnlyPrefix`、CTX ActivePrefixEpoch、PROMPT-014、ProviderLanguage bind-once、prompt-stability tests。
