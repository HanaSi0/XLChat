"""Package the installable plugin; source code is available in the repository."""
from pathlib import Path
import hashlib
import json
import xml.etree.ElementTree as ET
import zipfile

root=Path(__file__).resolve().parents[1]
dist=root/'dist'
dist.mkdir(exist_ok=True)
release=dist/'XLinUI.zip'
with zipfile.ZipFile(root/'bin/Release/XLinUI/latest.zip') as incoming, zipfile.ZipFile(release,'w',zipfile.ZIP_DEFLATED) as archive:
    for name in incoming.namelist():
        archive.writestr(name,incoming.read(name))
    archive.write(root/'README.md','README.md')
with zipfile.ZipFile(release) as archive:
    assert archive.testzip() is None
    manifest=json.loads(archive.read('XLinUI.json'))
    assert manifest['AssemblyVersion']==ET.parse(root/'XLinUI.csproj').getroot().findtext('.//Version')+'.0'
    assert manifest['DalamudApiLevel']==15 and manifest['InternalName']=='XLinUI'
    assert archive.read('XLinUI.dll')==(root/'bin/Release/XLinUI.dll').read_bytes()
    assert archive.read('StbImageSharp.dll')==(root/'bin/Release/StbImageSharp.dll').read_bytes(), 'Missing CPU image decoder'
(dist/'SHA256SUMS.txt').write_text(hashlib.sha256(release.read_bytes()).hexdigest()+'  XLinUI.zip\n')
print('XLinUI.zip',release.stat().st_size,'bytes; verified')
