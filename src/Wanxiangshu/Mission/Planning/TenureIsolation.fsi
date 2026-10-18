namespace Wanxiangshu.Mission.Planning

type ActiveTenureInfo =
    { WorkId: string
      IncumbencyId: string
      Stage: string
      OpeningCursor: int64
      IsFreshHandover: bool }

type TenureMessage =
    { Id: string option
      Role: string
      IsSynthetic: bool
      HasSemanticParts: bool
      Raw: obj }

type TenureAssemblyResult =
    { Messages: obj list
      ReanchorRequested: bool }

module TenureIsolation =
    val isCurrentTenurePhysical: openingCursor: int64 -> cursor: int64 option -> bool

    val assemble:
        messages: TenureMessage list ->
        tenure: ActiveTenureInfo ->
        messageCursors: Map<string, int64> ->
            TenureAssemblyResult
