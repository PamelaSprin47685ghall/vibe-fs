namespace Wanxiangshu.Context.Companion

open Wanxiangshu.Context.Companion.Blogger

[<RequireQualifiedAccess>]
module CompanionPrompt =
    val Normal: string
    val Squash: string
    val MemoryPreamble: string
    val asCommentedInstruction: lines: string list -> string
    val workingRecordMessage: frameBody: string -> string
    val previousTipMessage: tipField: string -> cycleId: string -> string
    val newWorkMessage: instructionLines: string list -> items: BloggerDeltaItem list -> string
    val companionMemoryBlock: preamble: string -> frozenRecordPrefix: string -> string
