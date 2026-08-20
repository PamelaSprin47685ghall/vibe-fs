namespace Wanxiangshu.Enforcer.InstitutionalLearning

open System
open Wanxiangshu.Enforcer
open Wanxiangshu.Host

[<RequireQualifiedAccess>]
module InstitutionalEnhancer =

    let rulebookRevision (rules: EnforcerRule list) =
        rules
        |> List.sortBy _.LexicalOrder
        |> List.map (fun rule -> rule.Name + "\u001f" + rule.EnforcerText + "\u001f" + rule.MainText)
        |> String.concat "\u001e"
        |> HostDigest.sha256Hex

    /// One bounded evaluation.  The conservative live implementation may
    /// absorb an experience into an explicitly named existing rule; otherwise
    /// it discards rather than inventing a permanent bilingual rule from
    /// insufficient evidence.  BIRTH remains a typed disposition for a future
    /// candidate that passes the behavior-diagnosis admission boundary.
    let evaluate (experience: string) (rules: EnforcerRule list) : LearningDisposition =
        let lower = experience.ToLowerInvariant()

        rules
        |> List.tryFind (fun rule -> lower.Contains(rule.Name.ToLowerInvariant(), StringComparison.Ordinal))
        |> Option.map (fun rule -> LearningDisposition.Absorb rule.Name)
        |> Option.defaultValue (LearningDisposition.Discard "no-reusable-mechanism")
