"""Generate/validate the public Dalamud custom repository from the plugin manifest."""
from pathlib import Path
import argparse
import json
import time
import zipfile

root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser()
parser.add_argument('--check',action='store_true')
args=parser.parse_args()
if args.check:
    with zipfile.ZipFile(root/'dist/XLinUI.zip') as archive:
        manifest=json.loads(archive.read('XLinUI.json'))
else:
    manifest=json.loads((root/'bin/Release/XLinUI/XLinUI.json').read_text())
version=manifest['AssemblyVersion']
assert version.endswith('.0') and manifest['InternalName']=='XLinUI'
assert manifest['DalamudApiLevel']==15
url=f"https://github.com/HanaSi0/XLinUI/releases/download/v{version[:-2]}/XLinUI.zip"
entry=dict(manifest)
entry.update(DownloadLinkInstall=url,DownloadLinkUpdate=url,DownloadLinkTesting=url,
             IsHide=False,IsTestingExclusive=False,LastUpdate=int(time.time()))
path=root/'pluginmaster.json'
if args.check:
    entries=json.loads(path.read_text())
    assert len(entries)==1 and isinstance(entries[0]['LastUpdate'],int)
    actual=entries[0].copy()
    actual.pop('LastUpdate')
    entry.pop('LastUpdate')
    assert actual==entry,'Repository and ZIP manifest do not match'
    print(f'Dalamud repository verified: {version}; API 15; matching install/update URLs')
else:
    path.write_text(json.dumps([entry],ensure_ascii=False,indent=2)+'\n')
    print(f'Generated pluginmaster.json: {version}')
