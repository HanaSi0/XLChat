from pathlib import Path
import zipfile
import json
import hashlib
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
release = root / 'XLinUI.zip'
source = root / 'XLinUI-source.zip'
with zipfile.ZipFile(root / 'bin/Release/XLinUI/latest.zip') as incoming, zipfile.ZipFile(release, 'w', zipfile.ZIP_DEFLATED) as archive:
    for name in incoming.namelist():
        archive.writestr(name, incoming.read(name))
    for name in ['README.md', 'DESIGN.md']:
        archive.write(root / name, name)
files = ['pluginmaster.json', 'tools/repository.py', 'NativeHotbarState.cs', 'HotbarPolicy.cs', 'NativeHotbarLayouts.cs', 'HotbarEditor.cs', 'TopBar.cs', 'TopBarLayout.cs', 'AppearanceAssets.cs', 'AppearanceSettings.cs', 'ImageEffects.cs', '.gitignore', 'Configuration.cs', 'ChatVisibilityState.cs', 'OriginalChatVisibility.cs', 'GameUiController.cs', 'NativeUiStyling.cs', 'NativeAppearanceState.cs', 'NativeTextState.cs', 'ReadableText.cs', 'SurfacePolicy.cs', 'ChatPolicy.cs', 'ChatProfile.cs', 'ChatInterface.cs', 'ChatCustomization.cs', 'HudGeometry.cs', 'HudReplacementPolicy.cs', 'CombatHud.cs', 'InventoryPanel.cs', 'QuestPanel.cs', 'GameWindowBrowser.cs', 'GameUiVisibilityPolicy.cs', 'UtilityPanels.cs', 'GameBridge.cs', 'XLinUI.csproj', 'Plugin.cs', 'UserInterface.cs', 'packages.lock.json', 'Build-Local.ps1', 'README.md', 'DESIGN.md', 'tools/package.py', '.github/workflows/publish-release.yml', 'tests/Visibility/Test.csproj', 'tests/Visibility/Program.cs', 'tests/Configuration/Test.csproj', 'tests/Configuration/Program.cs']
with zipfile.ZipFile(source, 'w', zipfile.ZIP_DEFLATED) as archive:
    for name in files:
        archive.write(root / name, name)
for path in [release, source]:
    with zipfile.ZipFile(path) as archive:
        assert archive.testzip() is None
        if path == release:
            manifest = json.loads(archive.read('XLinUI.json'))
            assert manifest['AssemblyVersion'] == ET.parse(root / 'XLinUI.csproj').getroot().findtext('.//Version') + '.0' and manifest['DalamudApiLevel'] == 15
            assert archive.read('XLinUI.dll') == (root / 'bin/Release/XLinUI.dll').read_bytes()
    print(path.name, path.stat().st_size, 'bytes; verified')
(root / 'SHA256SUMS.txt').write_text('\n'.join(hashlib.sha256(p.read_bytes()).hexdigest() + '  ' + p.name for p in [release, source]) + '\n')
