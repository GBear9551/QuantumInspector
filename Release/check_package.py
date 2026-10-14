"""Inspect the exported archive without importing or extracting it."""
from pathlib import Path
import tarfile
import hashlib
import json
import subprocess

root=Path(__file__).resolve().parents[1]
archive=root/'output/package/QuantumInspector-1.0.0.unitypackage'
expected=set((root/'Release/PackageContents.txt').read_text(encoding='utf-8-sig').splitlines())
with tarfile.open(archive,'r:gz') as package:
    members={m.name:m for m in package.getmembers()}
    paths={}
    for name,member in members.items():
        if name.endswith('/pathname'):
            paths[name.rsplit('/',1)[0]]=package.extractfile(member).read().decode('utf-8').strip()
    shipped={p for guid,p in paths.items() if guid+'/asset' in members}
    assert expected==shipped,(expected-shipped,shipped-expected)
    assert all(p.startswith('Assets/FightSongStudio/QuantumInspector/') for p in shipped)
    assert not any('/Verification/' in p or '~/' in p for p in shipped)
    for guid,path in paths.items():
        if path in shipped:
            assert guid+'/asset.meta' in members,path
            contents=package.extractfile(members[guid+'/asset']).read()
            assert contents==(root/path).read_bytes(),path
            if path.endswith('.cs'):
                text=contents.decode('utf-8-sig')
                assert 'System.Reflection' not in text and 'BindingFlags' not in text,path
old='Assets/InspectorTool/Editor/'
tracked=subprocess.check_output(['git','ls-files',old],cwd=root,text=True).splitlines()
preserved=0
for path in tracked:
    if path.endswith('.meta'):
        original=subprocess.check_output(['git','show','HEAD:'+path],cwd=root).decode('utf-8-sig')
        moved=(root/path.replace(old,'Assets/FightSongStudio/QuantumInspector/Editor/',1)).read_text(encoding='utf-8-sig')
        assert next(l for l in original.splitlines() if l.startswith('guid:'))==next(l for l in moved.splitlines() if l.startswith('guid:')),path
        preserved+=1
report={'archive':str(archive.relative_to(root)),'files':len(shipped),'bytes':archive.stat().st_size,'sha256':hashlib.sha256(archive.read_bytes()).hexdigest(),'trackedAssetGuidsPreserved':preserved,'manifestAndContentsMatch':True,'singleRoot':True,'developmentHelpersExcluded':True}
(root/'Release/PackageAudit.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,indent=2))
