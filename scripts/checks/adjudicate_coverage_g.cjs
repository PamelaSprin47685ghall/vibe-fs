const fs = require('fs');
const { resolve } = require('path');

const ledger = JSON.parse(fs.readFileSync('scripts/checks/migration-ledger.json','utf8'));
const G = ledger.coverage_backlog.CoverageG;
const manifest = JSON.parse(fs.readFileSync('scripts/checks/semantic-owners.json','utf8'));

const ownerMap = {};
manifest.ownership.forEach(e => { ownerMap[e.path] = e.owner; });

// read WHAT.md law IDs — accept "## OWNER-001" or "OWNER-001" anywhere on line
function readWhatIds(owner) {
  try {
    const lines = fs.readFileSync(resolve('requirements/'+owner+'/WHAT.md'),'utf8').split('\n');
    return lines.filter(l => {
      // strip leading markdown headers like ## or ###
      const stripped = l.replace(/^#+\s*/, '');
      return stripped.match(/^[A-Z][A-Z\-]+-\d+/);
    }).map(l => {
      const stripped = l.replace(/^#+\s*/, '');
      return stripped.split(/\s+/)[0].trim();
    });
  } catch(e) { return []; }
}

// read namespace + opens from first 40 lines
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
    cutoverMove.push({file:p, currOwner, ns, reason: !hasWhat ? 'no_what_coverage' : !nsMatch ? 'ns_mismatch' : 'cross_owner_import', topWhatId: whatIds[0] || 'NONE', openCount: opens.length});
  } else {
    provenKeep.push({file:p, currOwner, ns, reason:'all_four_conditions', topWhatId: whatIds[0]||'NONE'});
  }
});

const outDir = resolve('proposals');
if (!fs.existsSync(outDir)) fs.mkdirSync(outDir, {recursive:true});
fs.writeFileSync(resolve('proposals/coverage_g_adjudication.json'), JSON.stringify({total:G.length, provenKeep: provenKeep.length, cutoverMove: cutoverMove.length, provenKeepByOwner: Object.entries(provenKeep.reduce((a,e)=>{ (a[e.currOwner]=a[e.currOwner]||[]).push(e.file); return a; }, {})).sort((a,b)=>b[1].length-a[1].length), cutoverMove, provenKeepPaths: provenKeep.map(e=>e.file)}, null, 2));
fs.writeFileSync(resolve('proposals/coverage_g_adjudication.md'), `# CoverageG Adjudication\n\n${provenKeep.length} PROVEN-KEEP + ${cutoverMove.length} CUTOVER-MOVE = ${G.length}\n\nSee coverage_g_adjudication.json for full table.`);

// Output three lists
console.log('=== 1) CUTOVER-MOVE 清单 ===');
cutoverMove.forEach(e => console.log(e.file + ' | curr=' + e.currOwner + ' | reason=' + e.reason + ' | topWhatId=' + e.topWhatId + ' | ns=' + e.ns));

console.log('\n=== 2) PROVEN-KEEP 按 owner 分组 ===');
Object.entries(Object.entries(provenKeep.reduce((a,e)=>{ (a[e.currOwner]=a[e.currOwner]||[]).push(e.file); return a; }, {})).sort((a,b)=>b[1].length-a[1].length)).forEach(([o,f])=>{ console.log(o+' ('+f.length+'):'); f.forEach(fi => console.log('  '+fi)); });

console.log('\n=== 3) 汇总统计 ===');
console.log('PROVEN-KEEP:', provenKeep.length, '| CUTOVER-MOVE:', cutoverMove.length, '| Total:', G.length, '| match:', (provenKeep.length+cutoverMove.length)===G.length?'OK':'MISMATCH');
