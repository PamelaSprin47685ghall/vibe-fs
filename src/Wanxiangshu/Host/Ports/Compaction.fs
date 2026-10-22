namespace Wanxiangshu.Host.Ports

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// HNI P-09: product say in host compaction.
[<RequireQualifiedAccess>]
type CompactionContribution =
    | Nothing
    | Context of lines: string list
    | ReplacePrompt of prompt: string

[<RequireQualifiedAccess>]
type ContinueVerdict =
    | Allow
    | Deny of reason: string

type ICompactionGate =
    abstract OnCompacting: session: SessionId -> Task<CompactionContribution>
    abstract AutoContinue: session: SessionId -> Task<ContinueVerdict>
