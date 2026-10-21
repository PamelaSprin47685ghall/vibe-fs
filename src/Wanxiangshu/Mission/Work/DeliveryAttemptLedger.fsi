namespace Wanxiangshu.Mission.Work

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// 交付 attempt 台账（进程内）：交付已成立、呈现、中断的「已发出」与「已观察」
/// 四个可分别取证的事实，键是精确 attempt 围栏。
module DeliveryAttemptLedger =

    type Fence =
        { SessionId: SessionId
          ProviderRun: ProviderRunIdentity
          CallId: ToolCallId }

    type InterruptFacts =
        { Issued: bool
          Observed: Result<unit, string> option }

    type Record =
        { Fence: Fence
          Physical: PhysicalUserMessageId option
          Established: bool
          Presented: bool
          Interrupt: InterruptFacts
          InterruptTask: Task option
          Sequence: int }

    val recordEstablished: fence: Fence -> physical: PhysicalUserMessageId option -> unit
    val recordPresented: fence: Fence -> unit
    val tryFindEstablished: sessionId: SessionId -> physical: PhysicalUserMessageId option -> (Fence * Record) option

    /// 条款 3：以 durable 交付见证为准的交付成立读取（进程内台账只是快速路径）。
    /// 查不到见证返回 None——那是「无法证明成立」，不是「没交付过」。
    val tryFindDurableEstablished:
        journal: AgentJournal option -> sessionId: SessionId -> physical: PhysicalUserMessageId option -> Fence option

    val hasEstablished: sessionId: SessionId -> bool

    /// 本 session 是否存在任何一次已成立的交付（durable 见证口径，不绑定物理输入）。
    val hasDurableEstablished: journal: AgentJournal option -> sessionId: SessionId -> bool

    val recordInterruptIssued: fence: Fence -> issued: Task -> unit
    val recordInterruptObserved: fence: Fence -> outcome: Result<unit, string> -> unit
    val interruptFacts: fence: Fence -> InterruptFacts
    val activeInterruptTask: sessionId: SessionId -> providerRun: ProviderRunIdentity -> Task option
    val dropSession: sessionId: SessionId -> unit
