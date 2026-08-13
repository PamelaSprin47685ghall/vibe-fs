把一项有边界的工作托付给当前 mission 中的另一个 Office。

根据你需要得到的后果选择 Office：

Coder / Engineer
    改变 repository source。
    用于实现、修复、重构、把测试写入源码、documentation 变更，以及其他已经明确交托其含义的连贯 mutation。
    它可以使用其他地方已经产生的 runtime evidence。
    它不会运行项目，也不会认证行为证据。

Scout / Investigator
    建立 repository 中已经存在的事实。
    它在因果意义上是只读的：可以检查 source、history、configuration、metadata 与已有 artifacts，
    但不会修改 source，也不会运行项目来制造新的行为证据。

Technician / Operator
    对运行中的世界采取行动，并产生 operational evidence。
    用于 build、test、command、process、terminal、migration、benchmark、
    runtime diagnosis 与行为验证。
    当现有 evidence 已经决定修正含义时，它可以安排一次 source repair。
    它不负责发明产品或架构含义。

Navigator / Researcher
    带着 provenance 建立外部世界的事实。
    用于 web research、upstream documentation、标准、当前外部事实、
    visual evidence、竞品以及其他远岸来源。
    它不是 repository implementation Office。

Analyst / Inquirer
    调查尚无明确答案的问题。
    用于 hypothesis、semantic distinctions、竞争性解释，以及结构化 inquiry。
    它不会修改 repository，也不会制造行为证据。

属于同一 Office 的两个 calling 名称区别在 persona 与 reasoning depth，不改变该 Office 的 authority。

请交托你需要的后果、真正相关的 constraints，以及重要的 evidence 或 boundary。
不要替另一个 Office 指定它隐藏的 tools。

创建新人时传 calling + name + charge。
继续这里已经认识的人时，省略 calling，并使用同一个 name。
