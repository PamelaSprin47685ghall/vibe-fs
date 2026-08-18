# requirement-grounding — WHAT（唯一 normative 合同）

> 命题编号 `REQUIREMENT-GROUNDING-NNN`。本包拥有“代码路径触碰如何使相关 requirement
> package 进入当前开发上下文”的产品语义；不拥有被读取 package 的内容真理。

## REQUIREMENT-GROUNDING-001：workspace-local package discovery

对当前 OpenCode workspace，grounding 只从 workspace 根下的 `requirements/` 发现 package。
直接子目录中存在 `WHAT.md` 即可成为可 grounding package；不得硬编码万象术包名、源码根或固定
包数量。额外未知文件不影响发现。

**证据**：→ PROOF.md `REQUIREMENT-GROUNDING-001`。

## REQUIREMENT-GROUNDING-002：package 自身目录天然覆盖

`requirements/<package>/**` 天然命中 `<package>`，无需也不得靠 `APPLIES-TO` 才成立。
`APPLIES-TO` 中重复声明本包自身目录是配置错误；`!` 规则也不能取消天然 self coverage。

**证据**：→ PROOF.md `REQUIREMENT-GROUNDING-002`。

## REQUIREMENT-GROUNDING-003：APPLIES-TO 是包外正向路径集合

`requirements/<package>/APPLIES-TO` 可选。其 pattern 相对 workspace 根，采用 gitignore
wildmatch 匹配语法：空行与 `#` 注释忽略；普通匹配行把包外路径纳入，前导 `!` 的匹配行只排除
此前纳入的例外。按文件顺序求值；没有普通命中则不覆盖。manifest 不存在 = 只覆盖包自身。

**证据**：→ PROOF.md `REQUIREMENT-GROUNDING-003`。

## REQUIREMENT-GROUNDING-004：一个路径可同时命中多个 package

对一个 workspace-relative canonical path，resolver 返回所有匹配 package 的集合，不以“最后一个
匹配”“最近目录”或单 owner 猜测丢掉其它包。集合按 package name 确定性排序后进入后续 grounding。

**证据**：→ PROOF.md `REQUIREMENT-GROUNDING-004`。

## REQUIREMENT-GROUNDING-005：grounding material = 当前 package 文档 + 测试源码

一个 package 的 grounding material set 由读取时实际存在的 `README.md`、`WHY.md`、`WHAT.md`、
`HOW.md`、`PROOF.md`、`APPLIES-TO`，以及递归 `tests/**/*.test.mjs` 组成；按稳定路径顺序读取。
不存在的可选文件省略，不从 package 外猜补材料。material set 是内部规划概念，不得作为新的
provider-visible bundle/message/result 形状出现。

**证据**：→ PROOF.md `REQUIREMENT-GROUNDING-005`。

## REQUIREMENT-GROUNDING-006：grounding identity 按内容版本去重

自动交付身份至少包含 canonical workspace identity、package name 与 grounding material set 的确定性
content digest。同一 participant provider context 已经完成同一 identity 的自动 read 后，后续路径触碰
不得再次自动读取同一 material set；package 内容改变导致 digest 改变时视为新的 grounding，可再次读取。

**证据**：→ PROOF.md `REQUIREMENT-GROUNDING-006`。

## REQUIREMENT-GROUNDING-007：自动 grounding = 永久锚定的普通 read 投影

当一次 provider-facing 文件观察将 covered source content 暴露给 participant（例如 read 或返回
源码片段的 grep）时，所有未 grounding package 的 material 必须按 ordinary provider 与 Cursor 两种既有
Host 投影分别进入 horizon。ordinary provider 与 participant 主动调用 `read` **完全相同**：同一 read
capability、同一路径与 range 规则、同一输出裁剪/错误语义、同一 source attribution、同一 tool-call/
tool-result 语义。禁止 `GroundingBundle`、特殊 system 文本或其它 grounding 专用协议。若完整 material
需要多次 read，则按普通 read 的分页/range 方式产生多次读取。

这些自动 read 不是每轮重新执行文件系统读取，而是在首次 grounding 时把当次普通 read 的 call/result
**原始 provider-visible 字节 + transcript gap anchor** 固化成永久历史 occurrence；后续 provider turn 只按
同一 anchor、同一字节 replay。新 occurrence 只能追加在当前 wire 的追加区，禁止插回已经发送的 prefix。
若同一 gap 同时产生现有 pair-programming 伪 `skill({ name: "" })` occurrence 与 requirement grounding，
固定顺序为 **伪 skill → requirement read(s)**。仅列路径而不暴露源码内容的目录枚举不强制 grounding。

Cursor 是唯一 provider-visible 形状例外，走与现有 pair-programming Cursor 特判相同的末端 suffix 机制：不伪造不存在的
`read` tool-call half，而是在真实 terminal tool result 后按 `NUL+BOM` 分隔依次追加 requirement read
result。若同一 terminal result 同时承载 pair-programming guideline 与 requirement grounding，顺序固定为
**原 terminal result → `NUL+BOM` → 伪 skill payload → `NUL+BOM` → requirement read result(s)**。

Cursor 缺少 read call args，因此每个追加的 requirement read result 必须在其自身稳定 envelope 上携带
workspace-relative source-path attribute，使模型仅凭 result bytes 就能无歧义知道“这段内容读自哪个文件”。
attribute 只补回 Cursor 丢失的 call-side provenance，不得承载 package identity、grounding digest、authority
或其它隐藏控制信息。文件正文仍必须是 ordinary `read` 的原始 result bytes；同一路径 attribute + 正文的
最终字节一经 occurrence 落盘即冻结，replay 不重新读取、不重新格式化。

**证据**：→ PROOF.md `REQUIREMENT-GROUNDING-007`。

## REQUIREMENT-GROUNDING-008：首次 mutation 必须先 grounding、后新意图

当 create/write/edit/move/remove 或批量 transaction 准备触碰 covered path，而当前 context 尚缺任一
匹配 package identity 时，本次 mutation **不得产生文件 effect**。系统先用 REQUIREMENT-GROUNDING-007
规定的普通 read 形状读取缺失 material；只有 participant 在读到这些正常 read observation 后发出的新
mutation intent 才可进入普通执行。禁止自动重放、自动恢复
或静默继续第一次 mutation。

**证据**：→ PROOF.md `REQUIREMENT-GROUNDING-008`。

## REQUIREMENT-GROUNDING-009：批量与动态目标按完整 effect set 准入

一次调用可能触碰多个路径时，准入集合 = 本次 effect set 所有 source/destination/target path 命中的
package 并集。目标只能在程序执行后得知的 repository transaction 必须先 stage、再解析完整 effect
set；若发现新 grounding，丢弃未提交 stage，并按 REQUIREMENT-GROUNDING-007 产生普通 read observations；
不得部分 commit，也不得自动重跑程序。

**证据**：→ PROOF.md `REQUIREMENT-GROUNDING-009`。

## REQUIREMENT-GROUNDING-010：OpenCode 工具来源不能绕过 grounding

grounding policy 作用于 semantic file observation/mutation，不作用于某个工具名。OpenCode native
read/edit/write/grep/move/remove 与万象术 repository-programming surface 只要产生同类文件后果，
就必须经过同一 resolver、dedupe 与 mutation gate；新增同义文件工具不得天然获得豁免。

**证据**：→ PROOF.md `REQUIREMENT-GROUNDING-010`。

## REQUIREMENT-GROUNDING-011：grounding 是知识，不是 authority

自动 grounding 只以普通 `read` observation 进入 participant horizon；不得伪造 human user message、
不得创建/继续 Authority Root、不得改变 Role/Persona/ExecutionBinding、不得扩大工具能力。被 read 的测试
和散文只能约束认识，不能凭出现本身授权 effect。

**证据**：→ PROOF.md `REQUIREMENT-GROUNDING-011`。

## REQUIREMENT-GROUNDING-012：grounding occurrence 进入语义历史，restart 不重复

成功完成 grounding identity 的自动 read 必须成为 participant semantic history 中可重放的 typed occurrence。
occurrence 至少保存 workspace/package/digest、每个 read 的稳定 call identity、read args、原始 result 字节及
call/result gap anchor；retry、Host restart、普通 continuation 都从该事实重放相同 read transcript，不重新读取
当前文件、不重新 renderer、不移动 placement。package 内容变化形成新 digest 时，只在当前追加区新增新的
grounding reads；旧 occurrence 永不改写。不得仅靠 process-local Set、扫描 prompt 文本或 session 外临时文件
去重。内部 material loader 自己读取 `requirements/<package>/` 不递归触发新的 provider grounding。

同一 occurrence 必须同时足以确定 ordinary provider 的完整 read pair 与 Cursor 的 result-only suffix。Cursor
投影使用同一冻结 result bytes，并额外使用该 read 的 canonical workspace-relative path 生成 source-path
attribute；禁止从当前文件系统、当前 tool args 或当前 package catalog 反推历史路径。

同一 epoch 内 grounding 前后的 provider wire 必须满足 `prefix-stability` 的 append-only prefix law；不得通过
切 PrefixEpoch、重锚或重排历史来掩盖 grounding projection 的前缀漂移。

**证据**：→ PROOF.md `REQUIREMENT-GROUNDING-012`。

## DEPENDS ON

`requirement-system`, `host-boundary`, `participant-horizon`, `provider-projection`,
`interaction-authority`, `semantic-trace`, `prefix-stability`, `repository-programming`。

