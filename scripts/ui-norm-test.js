// Verifies shinden.html normalizers against server-shaped payloads
// (PascalCase keys + string enums, as Jellyfin JSON serializes them).
const fs = require('fs');

const html = fs.readFileSync(
  require('path').join(__dirname, '..', 'src', 'AniBridge', 'Configuration', 'shinden.html'), 'utf8');
const script = html.match(/<script[^>]*>([\s\S]*?)<\/script>/)[1];

const start = script.indexOf('function abPick');
const end = script.indexOf('function aniBridgeTimeAgo');
if (start < 0 || end < 0 || end <= start) { console.error('SLICE NOT FOUND'); process.exit(1); }
const src = script.slice(start, end);
eval(src);

const need = ['abPick', 'abNormItem', 'abNormOutcome', 'abNormReport', 'abNormStatus', 'abNormTest'];
for (const n of need) {
  if (typeof eval(n) === 'undefined') { console.error('NOT FOUND: ' + n); process.exit(1); }
}

let failures = 0;
function check(cond, label) {
  if (!cond) { console.error('FAIL: ' + label); failures++; }
  else { console.log('ok: ' + label); }
}

// 1. Server format: PascalCase + string enums (the user's broken case).
const serverPayload = {
  FinishedAt: '2026-09-29T09:14:12+02:00',
  DryRun: true,
  InProgress: true,
  Scope: 'Watching, Planned',
  Error: null,
  Result: {
    Items: [
      { Title: 'Black Torch', Status: 'Watching', Outcome: 'WouldAdd', Detail: 'Sonarr',
        SourceUrl: 'https://shinden.pl/x', AniListUrl: 'https://anilist.co/anime/187538', Hint: null },
      { Title: 'Dogulwang', Status: 'Planned', Outcome: 'Skipped', Detail: 'unrecognized title',
        SourceUrl: 'https://shinden.pl/y', AniListUrl: null,
        Hint: 'No confident AniList match' },
    ],
    Scanned: 2, Added: 0, AlreadyExists: 0, Skipped: 1, Failed: 0, WouldAdd: 1,
  },
};
const r = abNormReport(serverPayload);
check(r.result.scanned === 2, 'scanned=2 from string enums');
check(r.result.wouldAdd === 1 && r.result.skipped === 1, 'wouldAdd=1 skipped=1');
check(r.result.unknown === 0, 'unknown=0');
check(r.inProgress === true && r.dryRun === true, 'flags');
check(r.scope === 'Watching, Planned', 'scope');
check(r.result.items[0].outcome === 4 && r.result.items[1].outcome === 2, 'outcomes mapped to numbers');
check(r.result.items[0].status === 'Watching', 'status string kept');
check(r.result.items[1].hint.indexOf('AniList') >= 0, 'hint kept');

// 2. Plain numbers (vanilla System.Text.Json) still work.
const numeric = abNormReport({ finishedAt: '2026-09-29T09:14:12Z', dryRun: false,
  result: { items: [{ title: 'X', outcome: 3, detail: 'boom' }] } });
check(numeric.result.failed === 1 && numeric.result.items[0].outcome === 3, 'numeric outcome');
check(numeric.result.items[0].status === null, 'missing status -> null');

// 3. Unknown outcome string never disappears silently.
const weird = abNormReport({ FinishedAt: '2026-09-29T09:14:12Z',
  Result: { Items: [{ Title: 'Z', Outcome: 'SomethingNew' }] } });
check(weird.result.unknown === 1 && weird.result.items[0].outcome === -1, 'unknown bucket');
check(weird.result.items[0].outcomeRaw === 'SomethingNew', 'raw label kept');

// 4. Status + test + arr-ish status objects, both casings.
check(abNormStatus({ ShindenOk: true, SonarrOk: false, RadarrOk: false, DryRun: false }).shindenOk === true, 'status Pascal');
check(abNormStatus({ shindenOk: true }).sonarrOk === false, 'status camel');
check(abNormTest({ Ok: false, Message: 'Login failed' }).message === 'Login failed', 'test Pascal');
check(abNormTest({ ok: true, message: 'OK', entries: 5 }).entries === 5, 'test camel');

if (failures) { console.error(failures + ' FAILURES'); process.exit(1); }
console.log('ALL NORMALIZER CHECKS PASSED');
