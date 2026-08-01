namespace Wanxiangshu.Next.Domain

open Wanxiangshu.Next.Kernel

/// SSOT/16 — Student & Teacher 纯领域内核。
///
/// LEARN-015/016/017（角色与 tier 映射）、LEARN-018/019（工具面）、
/// LEARN-032/033/035/036（QA 路径与原子追加）、LEARN-050（请求种类工具面）。
/// 生产接线（teacher/return 工具、Teacher session、QA 落盘）受 Host canary 门禁。
module StudentTeacher =

    /// LEARN-015：新增两个 CanonicalRole（与 Kernel.Role 分离，避免改动既有枚举
    /// 波及全部 match——接线层在 Host 边界把它们映射进 Role 的派生判定）。
    [<RequireQualifiedAccess>]
    type StudentTeacherRole =
        | Student
        | Teacher

    /// LEARN-050：Student 的两个请求种类工具面。
    /// Student × StudentLearn   → { teacher }
    /// Student × StudentCompile → { read, glob, grep, write, edit, return }
    [<RequireQualifiedAccess>]
    type StudentTool =
        | Teacher
        | Read
        | Glob
        | Grep
        | Write
        | Edit
        | Return

    /// LEARN-050：Student 的工具面由请求种类原子决定。
    let studentToolsFor (kind: ProviderRequestKind) : Set<StudentTool> =
        match kind with
        | ProviderRequestKind.StudentLearn -> set [ StudentTool.Teacher ]
        | ProviderRequestKind.StudentCompile ->
            set
                [ StudentTool.Read
                  StudentTool.Glob
                  StudentTool.Grep
                  StudentTool.Write
                  StudentTool.Edit
                  StudentTool.Return ]
        // Student 只在这两个请求种类下工作；其他种类不是 Student 的语义面。
        | _ -> Set.empty

    /// LEARN-051：工具面 fail-closed——未知/非 Student 请求种类返回空工具集。
    let isStudentRequest (kind: ProviderRequestKind) : bool =
        match kind with
        | ProviderRequestKind.StudentLearn
        | ProviderRequestKind.StudentCompile -> true
        | _ -> false

    /// LEARN-017：Student 与 Teacher 同 tier 映射（默认建议）。
    /// FastStudent → FastTeacher；DeepStudent → DeepTeacher。
    let teacherTierFor (studentTier: AgentTier) : AgentTier =
        match studentTier with
        | AgentTier.Fast -> AgentTier.Fast
        | AgentTier.Deep -> AgentTier.Deep

    /// LEARN-016：Student/Teacher 的公开/内部 Agent 变体（实现建议）。
    /// Student 公开（fast-student / deep-student）；Teacher 内部（fast-teacher / deep-teacher）。
    let studentAgentName (tier: AgentTier) : string =
        match tier with
        | AgentTier.Fast -> "fast-student"
        | AgentTier.Deep -> "deep-student"

    let teacherAgentName (tier: AgentTier) : string =
        match tier with
        | AgentTier.Fast -> "fast-teacher"
        | AgentTier.Deep -> "deep-teacher"

    // ── QA.md（LEARN-032/033/035/036）────────────────────────────────────────

    /// LEARN-032：QA 路径不变量。目录位于当前项目可读范围、临时根、版本控制忽略。
    /// 具体路径是实现建议；这里承载结构校验（不执行文件 IO）。
    let isIgnoredTmpPath (path: string) : bool =
        // `.agent/.tmp/` 必须被版本控制忽略（LEARN-032 第 5 条）。
        path.Contains(".agent/.tmp/") || path.Contains(".agent/.tmp")

    /// LEARN-033：QA.md 内容完全非结构化——按真实发生顺序追加的自然语言字节流。
    /// 物理追加时只插入防止文本粘连所需的换行；框架不得写入分隔线/标题/标记。
    let appendEntry (existing: string) (entry: string) : string =
        if String.length existing = 0 then
            entry
        else
            // 防止文本粘连所需的最小换行（LEARN-033：换行不是协议，框架不解析）。
            existing.TrimEnd([| '\n' |]) + "\n\n" + entry

    /// LEARN-034：逐字保存——用户原始请求是 QA.md 的第一条内容（LEARN-071）。
    let prependUserRequest (userRequest: string) : string = userRequest

    /// LEARN-037：重复恢复——通过完整尾部字节比较避免明显重复；无法确定时
    /// 宁可保留重复文本，不得删除可能存在的知识。
    let dedupeTail (existing: string) (entry: string) : string =
        if existing.EndsWith(entry, System.StringComparison.Ordinal) then
            existing
        else
            appendEntry existing entry

    /// LEARN-035：先落盘后生效——每个输入在产生外部效果之前落盘。成对提交禁止。
    /// 本函数只表达追加顺序语义；落盘由接线层做（原子写）。
    type QaAppendOrder =
        | UserRequestFirst
        | StudentMessageBeforeSend
        | TeacherReturnBeforeDeliver

    /// LEARN-075：每个 Student Logical Run 单飞——同时最多一个 teacher 调用、
    /// 一个 Teacher provider run、一个 QA 写入、一个编译 continuation。
    /// 运行时拒绝异常并发，返回明确错误。
    type StudentRunConcurrency =
        | Idle
        | TeacherInFlight
        | CompileInFlight

    let mayStartTeacherCall (current: StudentRunConcurrency) : bool =
        match current with
        | StudentRunConcurrency.Idle -> true
        | StudentRunConcurrency.TeacherInFlight
        | StudentRunConcurrency.CompileInFlight -> false

    /// LEARN-024：最终 Student return 的执行顺序是硬约束——
    /// 删除 QA 并确认不存在 → 提交 terminal → 显示 message。
    /// 删除失败：不提交 terminal，return 以明确错误返回（重试幂等）。
    type ReturnDeleteOutcome =
        | Deleted
        | AlreadyAbsent
        | DeleteFailed of reason: string

    let returnMayProceed (outcome: ReturnDeleteOutcome) : bool =
        match outcome with
        | ReturnDeleteOutcome.Deleted
        | ReturnDeleteOutcome.AlreadyAbsent -> true
        | ReturnDeleteOutcome.DeleteFailed _ -> false
