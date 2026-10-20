namespace Wanxiangshu.Participant.Persona

open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type Persona =
    | Director
    | Lead
    | Engineer
    | Coder
    | Investigator
    | Operator
    | Researcher
    | Analyst
    | Auditor
    | Distiller
    | Curator
    | Planner

module internal IdentityLibrary =
    /// Stateless `wanxiangli/identity` object (CUT-07); one shared instance.
    val instance: obj

[<RequireQualifiedAccess>]
module Persona =
    val render: Persona -> string
    val internal ofCanonical: obj -> Persona option
    val tryParse: string -> Persona option

[<RequireQualifiedAccess>]
module PersonaCatalog =
    val persona: Role -> Persona
    val bookkeeperPersona: unit -> Persona
    val personaV1: Role -> string
    val bookkeeperPersonaV1: unit -> string
    val inheritFrom: string -> string
