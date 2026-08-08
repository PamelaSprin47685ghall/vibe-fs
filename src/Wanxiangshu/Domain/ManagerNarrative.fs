namespace Wanxiangshu.Domain

open System
open Wanxiangshu.Kernel

/// GLORY-014/064 + SURFACE-004: the ONLY owner of the Birth and Reawakening
/// narrative texts. Tests read these constants; they must never be spelled
/// again at a call site.
module ManagerNarrative =

    /// GLORY-014: frozen planning tail (instruction source; packaging is synthetic).
    /// Not [<Literal>]: Fable inlines Literal and drops the JS export (glory facade).
    let PlanningTail =
        "If I want to complete the request above, how should I work?\nHow should I define the final goal?\nYou may call several rounds of tools to investigate and research, but in the end simply output your answer as direct text. Do not perform any actual work. Do not call suicide."

    /// GLORY-064: frozen reawakening prefix (instruction source).
    let ReawakeningPrefix = "You awaken once more in the distant future."

    /// GLORY-015: the synthetic narrative source identity. Duplicate-injection
    /// detection keys on (sessionId, lifeId, messageId, source), never on text
    /// matching.
    [<Literal>]
    let BirthNarrativeSource = "manager-birth-planning-tail"

    /// One provider-visible narrative part. Synthetic guidance is marked so
    /// OpeningPromptRaw capture can exclude it (COMPANION-003).
    type NarrativePart =
        { Text: string
          Synthetic: bool }

    /// Structured Birth / Reawakening projection: human raw separate from
    /// synthetic instruction parts (SURFACE-004 packaging).
    type NarrativeProjection = { Parts: NarrativePart list }

    let private planningTailDocument =
        SyntheticToml.document
            [ "If I want to complete the request above, how should I work?"
              "How should I define the final goal?"
              "You may call several rounds of tools to investigate and research, but in the end simply output your answer as direct text. Do not perform any actual work. Do not call suicide." ]
            []

    let private reawakeningPrefixDocument =
        SyntheticToml.document [ ReawakeningPrefix ] []

    let private humanPart (text: string) : NarrativePart =
        { Text = text; Synthetic = false }

    let private syntheticPart (text: string) : NarrativePart =
        { Text = text; Synthetic = true }

    /// GLORY-014: first-Life Birth rewrite as multi-part projection.
    /// Provider-visible: [human raw] then [synthetic PlanningTail document].
    /// Durable Opening remains raw HumanRoot (capture before rewrite).
    let firstBirth (userTextRaw: string) : NarrativeProjection =
        { Parts =
            [ humanPart userTextRaw
              syntheticPart planningTailDocument ] }

    /// GLORY-064: reawakening rewrite after a completed Life.
    /// Provider-visible: [synthetic ReawakeningPrefix] + [human raw] + [synthetic PlanningTail].
    let reawakening (userTextRaw: string) : NarrativeProjection =
        { Parts =
            [ syntheticPart reawakeningPrefixDocument
              humanPart userTextRaw
              syntheticPart planningTailDocument ] }

    /// Compatibility: joined text view of a projection (tests / diagnostics).
    let renderText (projection: NarrativeProjection) =
        projection.Parts
        |> List.map (fun part -> part.Text.TrimEnd('\n'))
        |> String.concat "\n\n"
        |> fun s -> if s.EndsWith("\n") then s else s + "\n"
