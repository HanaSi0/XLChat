from pathlib import Path, PurePosixPath
import hashlib
import json
import xml.etree.ElementTree as ET
import zipfile

root=Path(__file__).resolve().parents[1]
built=root/'XLChat/bin/Release'
dist=root/'dist'
dist.mkdir(exist_ok=True)
release=dist/'XLChat.zip'
with zipfile.ZipFile(built/'XLChat/latest.zip') as incoming,zipfile.ZipFile(release,'w',zipfile.ZIP_DEFLATED) as archive:
    for name in incoming.namelist():
        assert not PurePosixPath(name).is_absolute() and '..' not in PurePosixPath(name).parts
        archive.writestr(name,incoming.read(name))
    for name in ['LICENCE','COPYRIGHT','README.md']:
        archive.write(root/name,name)
with zipfile.ZipFile(release) as archive:
    assert archive.testzip() is None
    manifest=json.loads(archive.read('XLChat.json'))
    assert manifest['AssemblyVersion']==ET.parse(root/'XLChat/XLChat.csproj').getroot().findtext('.//Version')+'.0'
    assert manifest['DalamudApiLevel']==15 and manifest['InternalName']=='XLChat' and manifest['Name']=='XLChat'
    assert archive.read('XLChat.dll')==(built/'XLChat.dll').read_bytes()
    required=['Microsoft.Data.Sqlite.dll','MessagePack.dll','Pidgin.dll','StbImageSharp.dll','SQLitePCLRaw.batteries_v2.dll','SQLitePCLRaw.core.dll','SQLitePCLRaw.provider.e_sqlite3.dll','WatsonWebserver.Lite.dll','SharedEmoteList.json','runtimes/win-x64/native/e_sqlite3.dll']
    assert all(name in archive.namelist() for name in required),'Missing Windows dependency'
    assert not any('XLinUI' in name or 'ChatTwo.dll' in name for name in archive.namelist())
(dist/'SHA256SUMS.txt').write_text(hashlib.sha256(release.read_bytes()).hexdigest()+'  XLChat.zip\n')
print('XLChat.zip',release.stat().st_size,'bytes; main DLL, metadata and Windows dependencies verified')
