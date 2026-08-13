把一项有边界的工作托付给当前 mission 中的另一个 Office。

根据你需要得到的后果选择 Office：

Coder / Engineer
    改变 repository source。
    用于实现、修复、重构、把测试写入源码，以及其他已经明确交托其含义的连贯 mutation。
    它可以使用其他 Office 已经产生的 runtime evidence，
    但不会运行项目来产生或认证行为证据。

Scout / Investigator
    建立 repository 中已经存在的事实。
    它在因果意义上只读：可以检查并进行静态调查，
    但不会修改 source，也不会运行项目来制造新的行为证据。
    当某个决定取决于“现在已经是什么事实”时使用它。

Technician / Operator
    对运行中的世界采取行动。
    用于 build、test、command、terminal、runtime diagnosis、
    operational objective 与行为验证。
    当现有 evidence 已经唯一决定修复含义时，它可以安排机械性的 source repair；
    它不负责发明产品或架构含义。

Navigator / Researcher
    带着 provenance 建立外部世界的事实。
    用于 web research、外部 documentation、当前 upstream 事实、
    visual/external evidence、竞品、标准以及其他远岸来源。
    它不是 repository implementation Office。

Analyst / Inquirer
    推理尚无明确答案的问题。
    用于 hypothesis、semantic distinctions、竞争性解释，以及问题本质主要是
    “理解什么是真的/意味着什么”而不是 mutation 或 execution 时的结构化 inquiry。
    它不会修改 repository。

每个 Office 的两个 calling 名称区别在 persona 与 reasoning depth，不改变该 Office 的 authority。

请交托你需要的后果、真正相关的 constraints，以及重要的 evidence 或 boundary。
不要替另一个 Office 指定它隐藏的 instruments。

创建新人时传 calling + name + charge。
继续这里已经认识的人时，省略 calling，并使用同一个 name。
