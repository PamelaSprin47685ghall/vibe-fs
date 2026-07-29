namespace Wanxiangshu.Next.Tests

open Xunit
open Wanxiangshu.Next.Kernel

module RolesTests =

    [<Fact>]
    let ``Orchestrator_role_permission_matrix`` () =
        let allowed = set [ ToolPermission.Fork; ToolPermission.Join ]

        Assert.Equal<ToolPermission Set>(allowed, Roles.permissions Role.Orchestrator)
        Assert.True(Roles.isAllowed Role.Orchestrator ToolPermission.Fork)
        Assert.True(Roles.isAllowed Role.Orchestrator ToolPermission.Join)
        Assert.False(Roles.isAllowed Role.Orchestrator ToolPermission.List)
        Assert.False(Roles.isAllowed Role.Orchestrator ToolPermission.Read)

    [<Fact>]
    let ``Manager_role_permission_matrix`` () =
        let allowed = set [ ToolPermission.Fork; ToolPermission.Join; ToolPermission.List ]

        Assert.equal<ToolPermission Set>(allowed, Roles.permissions Role.Manager)
        Assert.True(Roles.isAllowed Role.Manager ToolPermission.Fork)
        Assert.True(Roles.isAllowed Role.Manager ToolPermission.Join)
        Assert.True(Roles.isAllowed Role.Manager ToolPermission.List)

        Assert.False(Roles.isAllowed Role.Manager ToolPermission.Exec)
        Assert.False(Roles.isAllowed Role.Manager ToolPermission.Pty)
        Assert.False(Roles.isAllowed Role.Manager ToolPermission.Read)
        Assert.False(Roles.isAllowed Role.Manager ToolPermission.Network)
        Assert.False(Roles.isAllowed Role.Manager ToolPermission.Glob)
        Assert.False(Roles.isAllowed Role.Manager ToolPermission.Grep)
        Assert.False(Roles.isAllowed Role.Manager ToolPermission.Inspector)

    [<Fact>]
    let ``DevOps_role_permission_matrix`` () =
        let allowed =
            set
                [ ToolPermission.Pty
                  ToolPermission.Exec
                  ToolPermission.Join
                  ToolPermission.List
                  ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Inspector
                  ToolPermission.Coder ]

        Assert.equal<ToolPermission Set>(allowed, Roles.permissions Role.DevOps)
        Assert.True(Roles.isAllowed Role.DevOps ToolPermission.Pty)
        Assert.True(Roles.isAllowed Role.DevOps ToolPermission.Exec)
        Assert.True(Roles.isAllowed Role.DevOps ToolPermission.Join)
        Assert.True(Roles.isAllowed Role.DevOps ToolPermission.List)
        Assert.True(Roles.isAllowed Role.DevOps ToolPermission.Read)
        Assert.True(Roles.isAllowed Role.DevOps ToolPermission.Glob)
        Assert.True(Roles.isAllowed Role.DevOps ToolPermission.Grep)
        Assert.True(Roles.isAllowed Role.DevOps ToolPermission.Inspector)
        Assert.True(Roles.isAllowed Role.DevOps ToolPermission.Coder)

        Assert.False(Roles.isAllowed Role.DevOps ToolPermission.Fork)
        Assert.False(Roles.isAllowed Role.DevOps ToolPermission.Write)
        Assert.False(Roles.isAllowed Role.DevOps ToolPermission.Edit)
        Assert.False(Roles.isAllowed Role.DevOps ToolPermission.Verdict)

    [<Fact>]
    let ``Coder_role_permission_matrix`` () =
        let allowed =
            set
                [ ToolPermission.Read
                  ToolPermission.Write
                  ToolPermission.Edit
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Inspector ]

        Assert.Equal<ToolPermission Set>(allowed, Roles.permissions Role.Coder)
        Assert.True(Roles.isAllowed Role.Coder ToolPermission.Inspector)
        Assert.False(Roles.isAllowed Role.Coder ToolPermission.Exec)
        Assert.False(Roles.isAllowed Role.Coder ToolPermission.Fork)
        Assert.True(Roles.isAllowed Role.Coder ToolPermission.Read)

    [<Fact>]
    let ``Inspector_role_permission_matrix`` () =
        let allowed =
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Exec ]

        Assert.equal<ToolPermission Set>(allowed, Roles.permissions Role.Inspector)
        Assert.True(Roles.isAllowed Role.Inspector ToolPermission.Read)
        Assert.True(Roles.isAllowed Role.Inspector ToolPermission.Glob)
        Assert.True(Roles.isAllowed Role.Inspector ToolPermission.Grep)
        Assert.True(Roles.isAllowed Role.Inspector ToolPermission.Exec)

        Assert.False(Roles.isAllowed Role.Inspector ToolPermission.Write)
        Assert.False(Roles.isAllowed Role.Inspector ToolPermission.Edit)
        Assert.False(Roles.isAllowed Role.Inspector ToolPermission.Fork)
        Assert.False(Roles.isAllowed Role.Inspector ToolPermission.Pty)

    [<Fact>]
    let ``Browser_role_permission_matrix`` () =
        let allowed =
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Network ]

        Assert.Equal<ToolPermission Set>(allowed, Roles.permissions Role.Browser)
        Assert.True(Roles.isAllowed Role.Browser ToolPermission.Read)
        Assert.True(Roles.isAllowed Role.Browser ToolPermission.Glob)
        Assert.True(Roles.isAllowed Role.Browser ToolPermission.Grep)
        Assert.True(Roles.isAllowed Role.Browser ToolPermission.Network)

        Assert.False(Roles.isAllowed Role.Browser ToolPermission.Exec)
        Assert.False(Roles.isAllowed Role.Browser ToolPermission.Fork)

    [<Fact>]
    let ``Meditator_and_Reviewer_role_permission_matrix`` () =
        let allowed =
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Inspector ]

        Assert.Equal<ToolPermission Set>(allowed, Roles.permissions Role.Meditator)

        Assert.Equal<ToolPermission Set>(
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Inspector
                  ToolPermission.Verdict ],
            Roles.permissions Role.Reviewer
        )

        Assert.True(Roles.isAllowed Role.Meditator ToolPermission.Read)
        Assert.True(Roles.isAllowed Role.Meditator ToolPermission.Glob)
        Assert.True(Roles.isAllowed Role.Meditator ToolPermission.Grep)
        Assert.True(Roles.isAllowed Role.Meditator ToolPermission.Inspector)

        Assert.True(Roles.isAllowed Role.Reviewer ToolPermission.Read)
        Assert.True(Roles.isAllowed Role.Reviewer ToolPermission.Glob)
        Assert.True(Roles.isAllowed Role.Reviewer ToolPermission.Grep)
        Assert.True(Roles.isAllowed Role.Reviewer ToolPermission.Inspector)

        Assert.False(Roles.isAllowed Role.Meditator ToolPermission.Fork)
        Assert.False(Roles.isAllowed Role.Reviewer ToolPermission.Exec)
