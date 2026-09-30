namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// HOST-026 / PROMPT-017: project the session-bound ProviderLanguage onto the
/// Wanxiangshu-owned system-prompt segment without disturbing Host/AGENTS text.
open Wanxiangshu.Participant.Provider

module ProviderSystemTransform =
    [<Literal>]
    val replicaConstraintZh: string =
        "继续当前任务的只读查证，只使用当前可见且获准的工具。信息足够，或下一步需要写入、执行命令、向用户确认、给出结论或作出关键判断时，直接结束，不为继续调用而增加调查。对话里的 self_note 是先前对未来查证的展望，不是已经证实的结论，也不扩大权限。"

    [<Literal>]
    val replicaConstraintEn: string =
        "Continue the current task's read-only investigation using only the available, permitted tools. Stop when the evidence is sufficient or the next step requires a change, a command, user clarification, a conclusion, or a consequential judgment. Do not invent work to keep calling tools. A self_note in the conversation is an earlier outlook for investigation, not a verified conclusion or permission to do more."

    val replicaConstraintFor: lang: ProviderLanguage -> string

    val createWith:
        role: (SessionId -> Role option) ->
        isReplica: (SessionId -> bool) ->
        (obj -> obj -> Task<unit>)
