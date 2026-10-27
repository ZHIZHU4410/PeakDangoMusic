"""Build a manual installation ZIP from local outputs; never install or upload."""
from pathlib import Path
import argparse
import hashlib
import json
import shutil
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
VERSION = ET.parse(ROOT / 'source/PeakDangoMusic/PeakDangoMusic.csproj').findtext('PropertyGroup/Version')

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plugin-dll', type=Path, default=ROOT / 'source/PeakDangoMusic/bin/Release/netstandard2.1/PeakDangoMusic.dll')
    parser.add_argument('--native-exe', type=Path, default=ROOT / 'artifacts/native/PeakNeteaseBridge.exe')
    args = parser.parse_args()
    for binary in [args.plugin_dll, args.native_exe]:
        if not binary.is_file():
            parser.error('Missing build output: ' + str(binary) + '. Run build.ps1 first.')
    output = ROOT / 'artifacts'
    output.mkdir(exist_ok=True)
    archive = output / f'PeakDangoMusic-{VERSION}-完整安装包.zip'
    with tempfile.TemporaryDirectory(prefix='package-', dir=output) as temporary:
        stage = Path(temporary)
        plugins = stage / 'BepInEx/plugins'
        native = plugins / 'PeakDangoMusic/Native'
        native.mkdir(parents=True)
        shutil.copy2(args.plugin_dll, plugins / 'PeakDangoMusic.dll')
        shutil.copy2(args.native_exe, native / 'PeakNeteaseBridge.exe')
        for file in (ROOT / 'packaging/Native').iterdir():
            if file.is_file():
                shutil.copy2(file, native / file.name)
        for name in ['LICENSE', 'NOTICE.md', 'README.md', 'CHANGELOG.md', 'SORIHIKO-DEFAULTS.zh-CN.md', 'VERIFICATION.zh-CN.md', 'icon.png', 'manifest.json']:
            shutil.copy2(ROOT / name, stage / name)
        shutil.copy2(ROOT / 'packaging/安装说明.md', stage / '安装说明.md')
        for name in ['Install.ps1', 'Start-NetEaseLyrics.ps1']:
            text = (ROOT / 'packaging' / name).read_text(encoding='utf-8-sig')
            (stage / name).write_text(text, encoding='utf-8-sig')
        for name, script, extra in [('一键安装.cmd', 'Install.ps1', ''), ('卸载歌词.cmd', 'Install.ps1', ' -Uninstall'), ('启动网易云歌词连接.cmd', 'Start-NetEaseLyrics.ps1', '')]:
            command = '@echo off\r\npowershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0' + script + '"' + extra + ' %*\r\nset "result=%ERRORLEVEL%"\r\necho.\r\npause\r\nexit /b %result%\r\n'
            (stage / name).write_bytes(command.encode('ascii'))
        shutil.copytree(ROOT / 'source', stage / 'source', ignore=shutil.ignore_patterns('bin', 'obj', '__pycache__', '*.pyc', '*.user'))
        for folder in ['tools', 'packaging']:
            shutil.copytree(ROOT / folder, stage / folder, ignore=shutil.ignore_patterns('__pycache__', '*.pyc'))
        shutil.copy2(ROOT / 'build.ps1', stage / 'build.ps1')
        inventory = [dict(path=p.relative_to(stage).as_posix(), size=p.stat().st_size, sha256=digest(p)) for p in sorted(stage.rglob('*')) if p.is_file()]
        (stage / 'SHA256.json').write_text(json.dumps(inventory, ensure_ascii=False, indent=2), encoding='utf-8')
        with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as bundle:
            for item in sorted(stage.rglob('*')):
                if item.is_file():
                    bundle.write(item, item.relative_to(stage))
    archive.with_suffix('.sha256').write_text(digest(archive) + '  ' + archive.name + '\n', encoding='utf-8')
    print(archive)

if __name__ == '__main__':
    main()
