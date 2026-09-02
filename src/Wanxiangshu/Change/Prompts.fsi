namespace Wanxiangshu.Change

module OrchestratorPrompts =
    /// ORCH-007 conflict resumption, sent to the Manager session that already owns
    /// the task.
    val buildConflictResumePrompt: files: string list -> string
