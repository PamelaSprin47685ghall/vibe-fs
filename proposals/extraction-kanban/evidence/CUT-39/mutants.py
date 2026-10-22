import subprocess, os, shutil
root='/workspace/vfs-cut/wt/CUT-39-wxl'
src=f'{root}/src/advisor/AdvisorLaw.fs'
orig=open(src).read()
env=dict(os.environ, PATH='/workspace/vfs-cut/tools/node22/bin:'+os.environ['PATH'])
M=[
 ('M01 key drops length prefix','string anchor + ":" + string text.Length + ":" + text','string anchor + ":" + text'),
 ('M02 key is anchor only','string anchor + ":" + string text.Length + ":" + text','string anchor'),
 ('M03 deliveredMaterial = whole view','"deliveredMaterial" ==> (delta |> Array.map','"deliveredMaterial" ==> (pieces |> Array.map'),
 ('M04 anchors not distinct','|> Array.map (fun (a, _, _) -> a) |> Array.distinct)','|> Array.map (fun (a, _, _) -> a))'),
 ('M05 delta ignores delivered','pieces |> Array.filter (fun (_, _, key) -> not (seen.Contains key))','pieces'),
 ('M06 delta sorted by anchor','pieces |> Array.filter (fun (_, _, key) -> not (seen.Contains key))','pieces |> Array.filter (fun (_, _, key) -> not (seen.Contains key)) |> Array.sortBy (fun (a, _, _) -> a)'),
 ('M07 hasNewMaterial always true','"hasNewMaterial" ==> (delta.Length > 0)','"hasNewMaterial" ==> true'),
 ('M08 record replaces instead of union','            incoming |> Array.iter (fun key -> cursor.Add key |> ignore)','            cursor.Clear()\n            incoming |> Array.iter (fun key -> cursor.Add key |> ignore)'),
 ('M09 clear forgets every caller','member _.clear(caller: obj) : unit = cursors.Remove(callerOf caller) |> ignore','member _.clear(caller: obj) : unit = cursors.Clear()'),
 ('M10 answer takes first, not last','        |> Array.rev\n',''),
 ('M11 answer role case-sensitive','StringComparison.OrdinalIgnoreCase','StringComparison.Ordinal'),
 ('M12 answer accepts blank','&& not (String.IsNullOrWhiteSpace(unbox<string> text))',''),
 ('M13 answer accepts any role','isString role\n                   && String.Equals(unbox<string> role, "assistant", StringComparison.OrdinalIgnoreCase)\n                   && ','true && '),
 ('M14 anchor accepts negatives','Number.isInteger($0) && $0 >= 0','Number.isInteger($0)'),
]
res=[]
try:
  for name,a,b in M:
    assert orig.count(a)>=1,(name)
    open(src,'w').write(orig.replace(a,b,1))
    bl=subprocess.run(['node','scripts/build.mjs','advisor'],cwd=root,env=env,capture_output=True,text=True)
    if bl.returncode!=0: res.append((name,'KILLED(build)')); continue
    t=subprocess.run(['node','--test','src/advisor/tests/001.test.mjs'],cwd=root,env=env,capture_output=True,text=True,timeout=120)
    res.append((name,'KILLED' if t.returncode!=0 else 'SURVIVED'))
finally:
  open(src,'w').write(orig)
  subprocess.run(['node','scripts/build.mjs','advisor'],cwd=root,env=env,capture_output=True)
for r in res: print(*r,sep='\t')
