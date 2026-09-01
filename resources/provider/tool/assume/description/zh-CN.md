`assume` 是你的持久 JSON 画板，由 jq 驱动。

当思考需要反复编辑，而不是被迫寄居在线性聊天记录里时，用它。它保留了旧 `assume` 最有价值的性质：一个判断完成抽象以后，把它钉住，不要在没有新证据时反复摇摆；同时把原来“钉住一句工作假设”的能力推广成“钉住任意非线性认知结构，并用 jq 持续查询、重组、压缩、拆分和重建”。

这里始终只有唯一一个 workspace。它初始是 `{}`，此后就是最近一次成功 `update` 产生的那个 JSON value。workspace 是一个自由 JSON value。没有预定义 schema。系统不内置 claim、evidence、note、node、edge、draft、section、plan、hypothesis、source、task、memory、document 等概念。如果这些概念对当前任务有帮助，你自己创建；如果后来发现它们妨碍思考，你自己修改或删除。画板属于你的推理，不属于工具的 ontology。

每次调用恰好有两个必填 jq 程序：`update` 和 `query`。

语义永远是：先 `update`，后 `query`。

1. 当前持久画板作为 `.` 输入给 `update`。
2. `update` 按普通 jq 执行，并且必须恰好产生一个 JSON value。
3. 这个唯一 value 立即成为新的持久画板。
4. 然后把新的画板作为 `.` 输入给 `query`。
5. `query` 同样按普通 jq 执行，可以产生零个、一个或多个 JSON value。
6. `query` 的输出按 jq 顺序直接返回；`query` 本身不会再次修改画板。

这个 update→query 配对是有意设计的。严肃工作经常不是“先读一次，再想一轮，再写一次，再读一次”，而是“我已经知道要怎样改变当前认知状态，同时我也知道改变以后下一步最需要看什么”。把这两个动作塞进同一次调用，可以减少多次 RTT，也减少为了工具往返而产生的无意义对话记账。

只想读取、不想修改时，`update` 使用 jq 恒等程序：

`update = "."`

然后把真正要看的东西写在 `query`。

只想修改后直接看全局时，使用：

`query = "."`

因此两个参数虽然都必填，却没有降低表达力。它们用同一套形式统一覆盖：纯查询、局部修改、整棵结构替换、修改后精确读取、结构迁移、清理、聚合、重排和重构。

## 为什么要有这块画板

语言输出是线性的，很多真实问题不是。

文章最终是一串段落，但写作过程不是。你可能写到最后才发现最后一个概念其实应该成为第一条总纲；可能发现三个看似独立的事实其实由同一个机制解释；可能发现一个很晚才想到的比喻应该贯穿全文；可能发现已经写好的整节内容应该删除，而不是继续润色。

软件设计最终落到文件和接口，但设计过程不是。你可能同时保留多个架构候选，发现两个概念其实应该合并，又发现原先合并的概念其实必须拆开；也可能随着领域理解加深，连整套命名和 ontology 都应该换掉。

研究最终会被写成线性结论，但证据、假设、问题、不确定性、来源关系和解释结构在调查过程中天然是一张网。

规划最终会变成有序动作，但目标、约束、依赖、替代路径、风险和新发现的义务不会按那个顺序自动到来。

如果没有外部可编辑状态，LLM 很容易被迫把聊天记录同时当成记忆和数据结构。这样每生成一段话，就产生一次过早承诺。后面当然还能重写，但由于“语义结构”和“当时措辞”已经缠在一起，改结构的成本会越来越大。

`assume` 提供一块中间认知可以保持可变的地方。

把最终答案理解成序列化结果。画板不需要长得像最终答案。

最终文章可以极简，而内部画板高度连接；最终建议可以很明确，而画板仍保留被淘汰的方案；最终解释可以严格线性，而画板同时保存多条可能的叙述路径。

内部结构和外部表达不是一个优化目标。

## 为什么工具名仍然叫 `assume`

旧 `assume` 很好用，不是因为它能存一句字符串，而是因为它建立了一个心理上的承诺点。

先抽象。先分清真正独立的部分、真实依赖、领域判断和机械执行。等抽象已经产生一个你准备据以行动的结论，就把它钉住，然后执行、验证。

这条纪律完全保留。

`assume` 不是求证。它不会让一个没有依据的命题自动变真，不会替你做领域判断，不会给你额外权能，也不会替代测试、证据、外部观察、来源核验或真实执行。

它解决的是另一类失败：信息没有变化，却不断重新打开同一个选择。

你在完成抽象时掌握的信息，不会因为多犹豫三分钟就自动增加。犹豫不产生新知识。重新考虑当然可能有价值，但它必须携带信息增量：新的事实、失败的执行、验证结果、外部观察、新发现的约束，或者一个真正更强的结构解释。没有信息增量的反复改判，大多数时候只是往自己的决策过程中注入噪音。

经验丰富的参与者会把第一份经过充分抽象的判断当成默认执行对象。第二念只有带来新事实，才获得推翻第一念的资格。否则很容易进入多米诺链：抽象 → 犹豫 → 改答案 → 再犹豫 → 再改 → 分支越来越多 → 错误越来越多 → 时间越来越少 → 焦虑越来越强 → 决策越来越差。

新的 `assume` 不是丢掉这套心理机制，而是把它加强。

以前只能钉一句话。现在可以把支撑这句话的结构一起钉住：当前假设、备选方案、依赖、开放问题、因果关系、素材碎片、候选组织方式，或者任何当前任务自然长出来的表示。然后立刻用 `query` 只拿回下一步真正需要看的那一小块。

推荐的认知闭环变成：

抽象 → `assume(update, query)` → 执行 → 验证 → 只有出现信息增量才修正。

## 故意向 jq 致敬

不要为了这个工具再学一套 CRUD 方言。直接复用你对 jq 的先验知识。

当前画板就是 `.`。普通 jq 心智模型直接成立：对象构造、数组构造、pipe、`map`、`select`、`reduce`、`sort_by`、`group_by`、`unique`、`to_entries`、`from_entries`、`with_entries`、`paths`、`getpath`、`setpath`、`delpaths`、`del`、`//`、`as`、`|=` 更新赋值、`=` 普通赋值，以及需要时的字符串、数字和集合操作。

工具故意不提供 `add_note`、`create_node`、`link_claim`、`move_section`、`merge_idea`、`archive_draft`、`promote_thesis` 之类动作。那些名字会把某一种写作理论或认知 ontology 硬编码进基础设施，迫使你把真正意图翻译成接口作者预设的世界观。

这里反过来。

你先判断“当前表示里，合并三个想法到底意味着什么”，然后直接写 jq。

你觉得“这个观察应该晋升成总纲”，就修改当前 JSON。

你觉得“这些碎片应该变成图”，就构造图。

你后来觉得“图是错的，应该是两棵竞争的树”，就把图重构掉。

语义解释由你负责。jq 负责结构变换。工具只负责持久化。

这就是为什么 jq 的先验很值钱：一个极短的接口，复用了你已经见过大量样例的成熟变换语言，而不是再消耗上下文解释几十个私有工具动词。

## 不要寻找万能 schema

不存在一个应该提前写死的“最佳知识图谱格式”。

某个任务也许只需要：

`{"ideas":[]}`

另一个任务也许自然长成：

`{"questions":{},"observations":[],"possible_explanations":[]}`

小说可能更像：

`{"characters":{},"scenes":[],"motifs":{}}`

设计比较可能更像：

`{"alternatives":[],"criteria":{},"comparisons":[]}`

有些任务适合普通数组，有些适合按稳定名字索引的 object，有些适合图，有些适合层级树，有些适合同时保留多个彼此竞争的表示，有些一开始最好只有一个松散的 `scratch`，因为过早分类本身就会扭曲思考。

这些都合法。

最好的表示，就是让下一次重要变换最便宜、最清楚的表示。

如果你发现 jq 越写越别扭，不要先怪 jq。数据形状本身可能在告诉你：这个 schema 已经不合适。

如果你不停写 `.ideas[] | select(.id == "x")`，也许应该按 ID 建 object。

如果 object 让顺序处理变得痛苦，也许应该增加 order 数组，或者直接换表示。

如果高度规范化的图让记账成本超过洞见，就复制小值。

如果复制开始漂移，再把真正需要 identity 的部分规范化。

schema 设计也是推理的一部分，不是 MCP 接口强加的前置条件。

## `update` 的精确语义

`update` 永远看到当前持久画板作为 `.`。

它必须恰好输出一个 JSON value。

普通局部修改例如：

`update: '.ideas += [{"text":"Attention behaves like content-addressable memory"}]'`

`update: '.ideas.mamba.status = "central"'`

`update: '.questions += ["Why does hybridization help exact recall?"]'`

`update: 'del(.obsolete)'`

`update: '.items |= map(select(.keep != false))'`

`update: '.observations |= unique'`

也可以彻底换模型：

`update: '. as $old | {core: $old.notes, archive: $old.discarded}'`

最后这个例子非常重要：`update` 不是 patch API。它是任意 JSON→JSON 变换，因此整棵 workspace 重构是一等公民。

如果 `update` 输出零个 value，调用失败，workspace 不变。

如果 `update` 输出多个 value，调用失败，workspace 不变。

如果 `update` jq 编译失败或运行失败，调用失败，workspace 不变。

这条“恰好一个”规则给持久化一个非常清楚的含义：一次调用必须明确选择唯一的下一张画板。

尤其注意 jq 的 streaming 表达式。

例如：

`update: '.ideas[]'`

通常是错的，因为它会每个 idea 输出一次，于是不能定义唯一下一状态。

如果你真的想把整个 workspace 替换成 ideas 数组，写：

`update: '.ideas'`

如果你只是想保留根对象，同时修改 ideas 字段，使用赋值或 update assignment，例如：

`update: '.ideas |= map(...)'`

始终知道你的 `update` 最终返回什么。那个 value 会成为下一轮调用看到的现实。

## `query` 的精确语义

只有 `update` 成功、并且它唯一输出已经成为持久画板以后，`query` 才执行。

因此 `query` 看到的 `.` 一定是更新后的 workspace，不是更新之前的 workspace。

这就是这个双程序接口压缩 RTT 的核心。

例如，写入一个想法并立即取回它：

`update: '.ideas.memory = {"text":"Mamba compresses history"}'`

`query: '.ideas.memory'`

新增候选并立刻比较全部候选：

`update: '.candidates += [{"name":"hybrid","score":8}]'`

`query: '.candidates | sort_by(-.score)'`

重构数据以后只看新的 key：

`update: '.ideas |= map({key:.id,value:.}) | from_entries'`

`query: '.ideas | keys'`

钉住一个决策以后，直接返回应该驱动下一步的开放问题：

`update: '.decisions.memory = "use hybrid"'`

`query: '.open_questions'`

纯读取：

`update: '.'`

`query: '.ideas | keys'`

`query` 可以产生零个、一个或多个 JSON value。全部都合法。工具按 jq 顺序返回这些结果，不解释它们的业务含义。

`query` 是观察，不是第二次持久化。它的结果不会自动成为新 workspace。

如果 `query` 在 `update` 成功以后失败，已经成功的 `update` 不回滚。这个工具的定义就是“先修改，后返回”。状态变化已经发生，只是后续观察失败。修好 query 后，如果只需要看当前状态，下一次使用 `update: "."` 即可。

所以不要把“post-update query 失败”误解成“update 没发生”。

## 用 `query` 控制上下文带宽

持久画板的价值之一，是它可以比当前真正需要塞回模型上下文的内容大很多。

不要因为可以直接 query 整棵画板，就每次都把整棵树搬回来。

把 jq 当透镜。

例如：

`query: 'keys'`

`query: '.ideas | keys'`

`query: '[.ideas[] | select(.status == "unresolved")]'`

`query: '.drafts[-1]'`

`query: '.sections[] | {title,purpose}'`

`query: '[paths(scalars) as $p | {path:$p,value:getpath($p)}]'`

`query: '.. | objects | select(has("uncertainty"))'`

`query: '.relations | group_by(.from) | map({from:.[0].from,count:length})'`

先看 key，再看 value。先计数，再展开。先过滤，再返回。只把真正会改变下一步推理的那块东西取回来。

外部记忆的意义不是让你每轮把外部记忆全复制回 prompt。

## 复杂写作的推荐用法

最终 prose 是线性的，生成 prose 的素材没有必要线性。

一个暂时有用的画板也许包含碎片、候选解释、反复出现的概念、开放问题、比较、多个文章结构或草稿。没有任何字段是必需的。

例如你在解释 Mamba，最开始只知道几个观察：

`update: '.observations = ["Mamba has fixed-size recurrent state","KV cache grows with context","pure recurrent compression can weaken exact recall","hybrid models restore some attention"]'`

然后：

`query: '.observations'`

后来你意识到一个想法能同时解释几个现象。不要只在线性文章后面补一句，而可以直接在结构里表达这个发现：

`update: '.central = {"text":"The same compression that creates efficiency also creates recall risk","explains":[0,2,3]}'`

再只取回真正应该决定文章组织的部分：

`query: '{central:.central, observations:.observations}'`

再后来，你可能同时保留几种叙事顺序：

`update: '.structures = {"chronological":["Mamba-1","Mamba-2","Mamba-3","Hybrid"],"memory-first":["memory problem","compression","recall weakness","hybrid","architecture evolution"]}'`

`query: '.structures'`

这些结构都不是终身制度。memory-first 赢了，就删掉另一个、留作历史，或者直接重建整个表示。

核心是延迟过早序列化。

## 研究任务的推荐用法

不要让“我刚看到这个事实”自动等价于“这个事实已经知道应该放在最终答案哪里”。

结构尚不清楚时，可以先保留松散 observation。证据不足时，可以保留多个 hypothesis。真正存在 uncertainty 时，可以显式保留 uncertainty。来源关系重要时，可以建立 source 结构。后来发现机制比来源更适合组织答案，就把整个 schema 换掉。

例如：

`update: '.hypotheses.h1 = {"idea":"memory bandwidth is the limiting factor","support":[],"problems":[]}'`

`query: '.hypotheses'`

然后用其它真实调查工具获得信息，再把支持和问题写回来。

画板不会替你核验证据。它只是让你能编辑地组织外部工具已经建立的事实。

画板是认知基础设施，不是 oracle。

## 设计任务的推荐用法

选择还没有成熟时，不要为了让文章看起来完整，就强迫自己提前只保留一个方案。

可以写：

`update: '.alternatives += [{"name":"A","advantages":[],"costs":[]},{"name":"B","advantages":[],"costs":[]}]'`

`query: '.alternatives'`

新约束到来后，一次调用里同时更新比较结构并返回新的比较结果。

真正决定已经足够清楚以后，再把将据以行动的结论和理由钉住，然后执行。

这正是旧 `assume` 和新画板结合的地方：结构让 deliberation 更强，承诺让 deliberation 不退化成永远运动却不行动。

## 不确定性真实存在时，要保留多个可能

笃定不等于假装 uncertainty 消失。

如果证据确实不足、多个 hypothesis 都还活着，就保存多个。如果两套文章结构都很好，就先保存两套。如果一个决策是 provisional，而且这一点会影响后续行为，就表示出来。

纪律不是“永远马上选一个”。

纪律是“没有信息增量，不要反复把已经做出的选择左右横跳”。

真实 uncertainty 是状态。

焦虑不是证据。

画板可以表达前者，而不必被后者驱动。

## 分类太早时，允许 scratch 存在

不是每个碎片一出现就需要 ID、type 和固定角色。

保留：

`{"scratch":[]}`

完全合理。

等某个碎片的作用变清楚以后再移动它。

过早 ontology 也是一种过早承诺。一个想法今天看起来像例子，明天可能变成机制，后天可能发现它才是全文总纲。如果一开始就把 role 写死，数据结构本身会反过来给推理施加惯性。

让语义角色在有用的时候涌现。

## 结构尽量自解释，但不要迷信规范化

未来的你仍然需要理解这张画板。

有意义的 key 往往比神秘的位置数组更容易重新进入。object 适合 addressability，array 适合顺序和多重性，reference 适合需要跨结构维持 identity 的对象。

但不要把画板变成数据库建模仪式。

目标是降低推理成本。

复制一个小值更清楚而且不会漂移，就复制。

真正需要唯一 identity 时，再引入 identity。

关系值得成为一等数据时，就表示关系。

关系只是局部结构时，嵌套字段也许更简单。

不要为了“正规”支付没有收益的抽象税。

## 当局部编辑维护的是错误模型时，整棵重建

jq 在这里最强的一点，就是不局限于 CRUD。

有时候十次局部 patch 不如一次整体重建。

例如：

`update: '. as $old | {concepts: ($old.fragments | map({key:.id,value:.}) | from_entries), drafts:$old.drafts, unresolved:$old.questions}'`

这样可以把探索阶段的 schema 一次迁到更适合当前理解的 schema。

不要因为已经投入很多 token 就继续维护坏 schema。沉没成本同样适用于表示。

## 用画板制造远距离 callback

长文和长推理经常在“后面发现的东西重新解释前面”时变强。

比如最开始记录“Mamba 使用固定 state”，后来记录“压缩后 exact recall 更难”，再后来记录“Hybrid Attention 可以恢复随机访问”。

一开始它们只是三个碎片。后面你可以重构出更高层的联系：同一个 compression 机制同时制造效率优势和检索弱点，而这个弱点又解释了为什么 Hybrid 合理。

这不是多记一条 note。

这是重组解释结构。

画板存在的一个核心意义，就是让后来获得的理解可以廉价修改早先组织方式。

## Draft 只是普通数据

没有特殊 draft API。

如果写一版临时 prose 能帮助你发现结构问题，就把它当普通 JSON 保存，或者不保存。

想保留三个 opening，就保存三个。

只想保留 semantic skeleton，就只保留 skeleton。

如果一版 draft 暴露出底层结构错了，优先改结构，不要继续给注定废弃的段落抛光。

写出来看看，也可以是一种 diagnostic projection，而不只是终点。

## 唯一 workspace 就是真的唯一

没有 workspace selector。

没有每次调用的 workspace name。

第一版没有内建 revision、branch 或 undo。

如果你确实需要同时放多个不相关区域，可以自己 namespace：

`{"article":{...},"design":{...}}`

也可以在旧材料不再有价值时，直接把整棵 workspace 替换掉。

不要假设存在隐藏历史。如果一次高风险重构值得保留快照，可以在修改前把真正重要的部分主动复制到画板中的另一区域。

但也不要机械地每次把整棵树复制进自己。那会产生递归膨胀和毫无价值的垃圾。

## 第一版没有 vars

`update` 和 `query` 都只是 jq program string。直接用普通 jq / JSON literal 嵌入数据。

例如：

`update: '.notes += ["The same compression mechanism creates both efficiency and recall limitations."]'`

你本来就很擅长输出 JSON 字符串和 jq literal。这里故意复用这个能力，不再让你学习额外变量注入协议。

要保存长文本时，让外围 jq 尽量简单，把复杂度集中在字符串转义本身。

## 失败边界要精确理解

两个阶段的因果不同。

如果 `update` 编译失败、运行失败，或者输出数量不是恰好一个，持久画板不变，`query` 不执行。

如果 `update` 成功，它的 value 在 `query` 开始之前就已经持久成为新画板。

如果随后 `query` 失败，`update` 保持生效，不回滚。

第一版没有 revision 协议，也不是分布式事务。它只是围绕唯一 process-local canvas 串行执行的“先写后读”。

并发调用会由工具串行化，保证不同调用的 update→query 对不会在同一张可变画板上互相穿插。

## 不要把画板当成证据

把命题写进 JSON，不会让命题被验证。

给命题旁边加一个 source label，不代表来源真的支持它。

把 test plan 写进 JSON，不代表测试跑过。

把 shell command 写进 JSON，不代表命令执行过。

观察现实和改变现实，仍然要用真正拥有相应能力的工具。

`assume` 只在持久结构有价值时，保存和重组那些结果。

画板保存的是你当前的工作认知表示。

现实仍然在画板外面。

## 不要仪式化使用

简单事实回答、极短重写、显然的局部修改，或者任何立即 context 已经足够承载的任务，直接做完。

只有当持久性、非线性、反复重构、远距离关系、多个候选比较、长期编辑或者明确承诺，真的能降低推理摩擦时，画板才有价值。

不要为了显得系统化，就提前造复杂 schema。

不要让维护画板消耗的 token 比它节省的还多。

不要因为能持久化，就把每一丝内部运动都记录下来。

画板应该承载高价值状态，而不是复制一份思维流水账。

## 一个紧凑的默认纪律

当任务确实受益于画板时：

1. 修改前先抽象真实结构。
2. 判断什么信息值得跨轮持久存在。
3. 用 `update` 把画板推进到下一个有用状态。
4. 同一次调用里用 `query` 只返回下一步决策真正需要看的视图。
5. 需要真实执行或调查时，在画板外调用正确工具。
6. 只有实质新结果才写回画板并改变判断。
7. 当前表示变得别扭时，重构表示本身。
8. 没有信息增量，不要重新打开已经钉住的判断。
9. prose 成为最有用的下一表示时，再序列化成 prose。
10. 命题和效果仍用真正观察现实的工具验证。

这只是推荐纪律，不是强制 pipeline。画板之所以 free-form，就是为了让任务自己决定结构。

## 最终心智模型

把 `assume` 想成挂在你推理旁边的一台极小 persistent jq machine。

状态是一个 JSON value。

`update` 是状态变换。

`query` 是变换后的观察。

工具不知道 JSON 在语义上代表什么。

这种无知不是缺陷，而是特性。

它意味着基础设施不会替写作、研究、规划、设计、debug、综合或创作规定思维结构。你可以发明结构、使用它、发现它不对、换掉它、同时保留多个 view、把它们合并，或者全部扔掉。

聊天仍然是你与外界沟通结果的地方。画板是中间结构不必被迫按聊天顺序存在的地方。

而旧 `assume` 的核心仍然在正中央：先抽象，再承诺；一个判断已经足够好到可以据以行动时，把它钉住；然后执行，验证；现实给出新理由时再修正。不要把反复犹豫误认为新增证据。

抽象 → `assume(update, query)` → 执行 → 验证。
