namespace Wanxiangshu.Context.Companion

/// Context-compression projection owner. Prompt wrappers, synthetic identities
/// and the provider-visible Blogger message plan cross as plain JSON only.
[<RequireQualifiedAccess>]
module CompanionProjectionSurface =

    val normalInstructionLines: string array
    val normalInstruction: string
    val memoryPreamble: string
    val normal: string

    val workingRecord: body: string -> string
    val previousTip: fieldName: string -> cycleId: string -> string
    val newWork: items: obj array -> string
    val memoryBlock: body: string -> string
    val sealRoot: sha256: obj -> value: obj -> string
    val companionMemoryMessageId: sha256: obj -> seal: string -> string
    val frameMessageId: sha256: obj -> value: obj -> string
    val instructionMessageId: sha256: obj -> value: obj -> string

    /// Build one normal request from durable frame bodies and the physical delta.
    val build: sha256: obj -> value: obj -> obj

    val projectionIntent: sha256: obj -> value: obj -> obj
