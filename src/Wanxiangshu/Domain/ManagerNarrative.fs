namespace Wanxiangshu.Domain

open System
open Wanxiangshu.Kernel

/// GLORY-014/064/074 + SURFACE-004: BlindPlan lifecycle text owner.
/// Legacy PlanningTail bytes stay frozen for decode; production Birth /
/// Reawakening use Planning Table (§7.4.1).
module ManagerNarrative =

    /// GLORY-014: legacy planning tail — inert decode / migration only.
    let PlanningTail =
        "If I want to complete the request above, how should I work?\nHow should I define the final goal?\nYou may call several rounds of tools to investigate and research, but in the end simply output your answer as direct text. Do not perform any actual work. Do not call suicide."

    /// GLORY-064: legacy reawakening prefix — inert decode / migration only.
    let ReawakeningPrefix = "You awaken once more in the distant future."

    /// GLORY-015: synthetic narrative source identity for duplicate detection.
    [<Literal>]
    let BirthNarrativeSource = "manager-birth-planning-table"

    type NarrativePart = { Text: string; Synthetic: bool }

    type NarrativeProjection = { Parts: NarrativePart list }

    let private planningTableLines =
        [ "The Planning Table"
          ""
          "A request has arrived."
          ""
          "Prepare an honest account of the road it requires before that road is entrusted."
          ""
          "Imagine that another Manager will have to carry this work after you leave the table."
          ""
          "They will inherit every obligation you omit."
          "They will pay for every vague dependency."
          "They will discover every task you quietly left unnamed."
          ""
          "Plan for that person, not for convenience."
          ""
          "Ask what must become true for the request to be genuinely complete."
          ""
          "Account for the work, evidence, dependencies, uncertainties, and risks that a competent Manager would need to carry."
          ""
          "Do not make the road shorter merely because it looks difficult."
          "Do not make it longer merely to appear thorough."
          ""
          "Independent obligations may be independent."
          "Dependencies should be real dependencies."
          "Do not invent order where the work itself supplies none."
          ""
          "You may investigate when investigation is necessary to make the account truthful."
          ""
          "Investigation serves the account."
          "Do not begin carrying out the work you are planning."
          ""
          "When the account is complete enough that another Manager could receive it without having to guess what you omitted, write it with todowrite."
          ""
          "Write the plan you would be willing to hand to someone else and then hold them to." ]

    let private t1RevelationLines =
        [ "The account has been accepted."
          ""
          "Keep the standard you used while preparing it."
          ""
          "Until this moment, you were asked to make the road honest for the Manager who would have to carry every obligation you named and every omission you allowed."
          ""
          "That distance mattered."
          "It kept convenience from bargaining with the plan before the plan was committed."
          ""
          "The Manager who will carry it is you."
          ""
          "The road is yours."
          ""
          "Do not lower the standard now that you know whose time, attention, and effort it will cost."
          ""
          "Change the account when reality changes it: when evidence reveals new work, when an obligation is genuinely discharged, or when the shape of the mission becomes clearer."
          ""
          "Do not change it merely to make the road look shorter."
          ""
          "Carry out what you have just entrusted to another."
          ""
          "Planning is not completion."
          "Difficulty is not impossibility."
          "You have time."
          ""
          "Begin." ]

    let private reawakeningBlindPlanLines =
        [ "You awaken once more in the distant future."
          ""
          "Another request has arrived."
          ""
          "Before anyone carries it, prepare the road for the Manager who will." ]

    let private livingMissionLines =
        [ "Keep the mission's living obligations truthful with todowrite."
          ""
          "Change the account when the work, evidence, or genuine decomposition has changed."
          ""
          "Do not remove an obligation merely because you want the road to look shorter."
          ""
          "Do not preserve an obligation merely because it once appeared in the plan after the work has genuinely discharged it."
          ""
          "While something is in flight or being judged, continue useful independent work."
          ""
          "Wait only when the next useful action truly depends on what has not yet arrived."
          ""
          "Each accepted account supersedes the previous one as your present statement of what the mission still owes." ]

    let private preT1IdleLines =
        [ "The account is not yet ready to entrust."
          ""
          "You have time."
          "Make the road honest enough that another Manager would not have to guess what you omitted."
          ""
          "Write it with todowrite when it is ready." ]

    let private postT1IdleLines =
        [ "You have done useful work, and useful action may still remain."
          ""
          "Time spent is not time exhausted."
          "A long road is still a road."
          ""
          "Look again at what the mission still owes."
          ""
          "If useful action remains, continue."
          "When nothing useful remains, seek your end." ]

    let planningTableDocument = SyntheticToml.document planningTableLines []

    let t1RevelationDocument = SyntheticToml.document t1RevelationLines []

    let livingMissionDocument = SyntheticToml.document livingMissionLines []

    let preT1IdleDocument = SyntheticToml.document preT1IdleLines []

    let postT1IdleDocument = SyntheticToml.document postT1IdleLines []

    let preT1ForkTaken (byname: string) =
        SyntheticToml.document
            [ sprintf "%s has taken your question." byname
              ""
              "You are still preparing the road for another Manager."
              "Use this investigation to improve the plan."
              "Do not begin carrying out the plan yourself."
              "When the account is ready to entrust, write it with todowrite." ]
            []

    let preT1ForkReturned (byname: string) =
        SyntheticToml.document
            [ sprintf "%s has returned." byname
              ""
              "You are still at the Planning Table."
              "Use what was learned to make the account more truthful."
              "Do not begin carrying the road before the account is entrusted." ]
            []

    /// TODO-015 / GLORY-074: canonical T1 tool result = entrustment revelation + enriched todo body.
    let wrapT1AcceptedResult (todoWriteResult: string) =
        let normalized = SyntheticToml.normalizeNewlines todoWriteResult

        if String.IsNullOrWhiteSpace normalized then
            t1RevelationDocument
        else
            t1RevelationDocument.TrimEnd('\n') + "\n\n" + normalized

    let private humanPart (text: string) : NarrativePart = { Text = text; Synthetic = false }

    let private syntheticPart (text: string) : NarrativePart = { Text = text; Synthetic = true }

    /// GLORY-074: first-Life BlindPlan Opening — [human raw] + Planning Table.
    let firstBirth (userTextRaw: string) : NarrativeProjection =
        { Parts = [ humanPart userTextRaw; syntheticPart planningTableDocument ] }

    /// GLORY-074: Reawakening — BlindPlan prefix + human raw + Planning Table.
    let reawakening (userTextRaw: string) : NarrativeProjection =
        { Parts =
            [ syntheticPart (SyntheticToml.document reawakeningBlindPlanLines [])
              humanPart userTextRaw
              syntheticPart planningTableDocument ] }

    let renderText (projection: NarrativeProjection) =
        projection.Parts
        |> List.map (fun part -> part.Text.TrimEnd('\n'))
        |> String.concat "\n\n"
        |> fun s -> if s.EndsWith("\n") then s else s + "\n"
