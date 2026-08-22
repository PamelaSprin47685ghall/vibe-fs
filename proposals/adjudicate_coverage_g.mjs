const fs = require('fs');
const { resolve } = require('path');

const ledger = JSON.parse(fs.readFileSync('scripts/checks/migration-ledger.json','utf8'));
const G = ledger.coverage_backlog.CoverageG;

const ownersManifest = JSON.parse(fs.readFileSync('scripts/checks/semantic-owners.json','utf8'));
const ownerMap = {};
ownersManifest.ownership.forEach(e => { ownerMap[e.path] = e.owner; });

function readWhatIds(owner) {
  try {
    const lines = fs.readFileSync(resolve('requirements/'+owner+'/WHAT.md'),'utf8').split('\n');
    return lines.filter(l => l.match(/^[A-Z][A-Z\-]+-\d+/)).map(l => l.split(/\s+/)[0].trim());
  } catch(e) { return []; }
}

function readNs(file) {
  try {
    const lines = fs.readFileSync(file,'utf8').split('\n').slice(0,40);
    const nsLine = lines.find(l => l.trim().startsWith('namespace '));
    const ns = nsLine ? nsLine.replace('namespace ','').trim() : 'NO-NS';
    const opens = lines.filter(l => /^\s*open\s+/.test(l)).map(l => l.trim().replace(/\s+/g,' '));
    return { ns, opens };
  } catch(e) { return { ns: 'ERROR', opens: [] }; }
}

const ownersInG = [...new Set(G.map(p => ownerMap[p] || 'UNKNOWN'))];
const whatMap = {};
ownersInG.forEach(o => { whatMap[o] = readWhatIds(o); });

const provenKeep = [];
const cutoverMove = [];

G.forEach(p => {
  const currOwner = ownerMap[p] || 'UNKNOWN';
  const { ns, opens } = readNs(p);
  const nsSeg = ns.replace('Wanxiangshu.','').split('.')[0].toLowerCase().replace(/[^a-z]/g,'-');
  const whatIds = whatMap[currOwner] || [];
  const hasWhat = whatIds.length > 0;
  const nsMatch = nsSeg === currOwner;
  const crossOwnerImport = opens.some(o => {
    const m = o.match(/open\s+Wanxiangshu\.([^.]+)\./);
    return m && m[1] !== currOwner;
  });

  if (!hasWhat || !nsMatch || crossOwnerImport) {
    cutoverMove.push({file:p, currOwner, ns, nsSeg, reason: !hasWhat ? 'no_what_coverage' : !nsMatch ? 'ns_mismatch' : 'cross_owner_import', topWhatId: whatIds[0] || 'NONE'});
  } else {
    provenKeep.push({file:p, currOwner, ns, nsSeg, reason:'all_four_conditions', topWhatId: whatIds[0]||'NONE'});
  }
});

const outDir = resolve('proposals');
if (!fs.existsSync(outDir)) fs.mkdirSync(outDir, {recursive:true});
fs.writeFileSync(resolve('proposals/coverage_g_adjudication.json'), JSON.stringify({total:G.length, provenKeep: provenKeep.length, cutoverMove: cutoverMove.length, provenKeepByOwner: Object.entries(provenKeep.reduce((a,e)=>{ (a[e.currOwner]=a[e.currOwner]||[]).push(e.file); return a; }, {})).sort((a,b)=>b[1].length-a[1].length), cutoverMove, provenKeepPaths: provenKeep.map(e=>e.file)}, null, 2));

console.log('=== 1) CUTOVER-MOVE 清单 ===');
cutoverMove.forEach(e => console.log(e.file + ' | curr=' + e.currOwner + ' | reason=' + e.reason + ' | topWhatId=' + e.topWhatId));

console.log('\n=== 2) PROVEN-KEEP 按 owner 分组 ===');
const byOwner = Object.entries(provenKeep.reduce((a,e)=>{ (a[e.currOwner]=a[e.currOwner]||[]).push(e.file); return a; }, {})).sort((a,b)=>b[1].length-a[1].length);
byOwner.forEach(([owner, files]) => { console.log(owner + ' (' + files.length + '):'); files.forEach(f => console.log('  ' + f)); });

console.log('\n=== 3) 汇总统计 ===');
console.log('PROVEN-KEEP:', provenKeep.length, '| CUTOVER-MOVE:', cutoverMove.length, '| Total:', G.length, '| match:', (provenKeep.length+cutoverMove.length)===G.length?'OK':'MISMATCH');

console.log('\n=== 4) 新增 owner 依赖边（acyclicity 检查）===');
const newEdges = [];
ownersManifest.ownership.forEach(e => {
  if (G.includes(e.path)) {
    try {
      const lines = fs.readFileSync(e.path,'utf8').split('\n').slice(0,40);
      lines.filter(l => /^\s*open\s+/.test(l)).forEach(l => {
        const m = l.match(/open\s+Wanxiangshu\.([^.]+)\./);
        if (m && m[1] !== e.owner) newEdges.push({from: e.owner, to: m[1], file: e.path});
      });
    } catch(e) {}
  }
});
console.log('新增跨 owner 依赖边数量:', newEdges.length);
newEdges.slice(0,20).forEach(e => console.log('  ' + e.from + ' -> ' + e.to + ' (' + e.file + ')'));
console.log('（超出 20 条只显示前 20，完整见 proposals/coverage_g_adjudication.json）');

console.log('\n=== 5) 计数确认 ===');
console.log('provenKeep.count:', provenKeep.length);
console.log('cutoverMove.count:', cutoverMove.length);
console.log('sum:', provenKeep.length + cutoverMove.length, '=== 124:', provenKeep.length + cutoverMove.length === 124 ? 'OK' : 'MISMATCH');
