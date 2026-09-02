namespace Wanxiangshu.Mission.Finality

/// JSON-native owner surface for Manager and Finality provider prose.
///
/// Narrative and rendering semantics remain in ManagerNarrative / FinalityPrompt;
/// resource loading remains ProviderResources / ProviderProse. This module only
/// closes that production boundary for semantic tests without exposing Fable
/// records, lists, or unions.
[<RequireQualifiedAccess>]
module PromptSurface =

    val reawakeningPrefix: unit -> string

    val planningTableDocument: unit -> string

    val t1RevelationDocument: unit -> string

    val wrapT1AcceptedResult: todoWriteResult: string -> string

    val firstBirth: userTextRaw: string -> obj

    val reawakening: userTextRaw: string -> obj

    val firstBirthText: userTextRaw: string -> string

    val reawakeningText: userTextRaw: string -> string

    val workActivation: unit -> string

    val idleEncouragementPreT1: unit -> string

    val idleEncouragementPostT1: unit -> string

    val rejected: reviewerWorkRecord: string -> string

    val blessed: workRecordBundle: string -> string

    val rest: unit -> string

    val managerSystemPrompt: unit -> string

    val reviewerSystemPrompt: unit -> string
