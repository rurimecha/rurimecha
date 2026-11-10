#!/usr/bin/env python3
"""Prepare release attachments locally from an explicit artifact allowlist."""
import argparse
import base64
from urllib.parse import urlsplit
import hashlib
import json
from pathlib import Path
import re
import shutil
import tarfile
import tempfile
import zipfile

parser = argparse.ArgumentParser()
parser.add_argument('--version', required=True)
parser.add_argument('--platform', choices=('linux-amd64', 'windows-x64'), required=True)
parser.add_argument('--core-package', type=Path, required=True)
parser.add_argument('--web', type=Path, required=True)
parser.add_argument('--apk', type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
for value in (args.version, args.platform):
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]*', value):
        parser.error('version and platform must be simple filename components')
output = root / 'releases' / args.version / args.platform
if output.exists():
    parser.error(f'output already exists: {output}')

def read_json(path):
    try:
        return json.loads(path.read_text())
    except (OSError, ValueError) as exc:
        parser.error(str(exc))

def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()

def artifact_files(directory):
    files = {}
    for path in sorted(directory.rglob('*')):
        if path.is_symlink() or path.name == '.git' or path.suffix in ('.go', '.dart', '.kt', '.kts', '.jks', '.keystore'):
            parser.error(f'non-artifact input: {path}')
        if path.is_file():
            files[path.relative_to(directory).as_posix()] = digest(path)
    return files

info = read_json(args.core_package / 'build-info.json')
web = read_json(args.web / 'frontend-release.json')
try:
    issuer = urlsplit(info['license']['issuer_url'])
    public_key = base64.b64decode(info['license']['public_key'] + '=', altchars=b'-_', validate=True)
    if issuer.scheme != 'https' or not issuer.hostname or issuer.username or issuer.password or issuer.query or issuer.fragment or len(public_key) != 65 or public_key[0] != 4 or not info['license']['build_time']:
        parser.error('official releases require public activation-center configuration')
    if info['version'] != args.version or info['platform'] != args.platform or web['format'] != 1 or web.get('ai') is not False:
        parser.error('version or web manifest format mismatch')
    for component in ('core', 'frontend'):
        if info[component]['dirty'] is not False or not re.fullmatch('[0-9a-f]{40}', info[component]['revision']):
            parser.error('official releases require clean, committed source revisions')
    if web['dirty'] is not False or info['frontend']['revision'] != web['revision']:
        parser.error('embedded and standalone frontend revisions must match and be clean')
    files = artifact_files(args.web)
    files.pop('frontend-release.json', None)
    if files != web['files']:
        parser.error('web asset hashes do not match manifest')
    for required in ('index.html', 'main.dart.js', 'flutter_bootstrap.js'):
        if required not in files:
            parser.error(f'missing web asset: {required}')
    if not any(name.startswith('assets/') for name in files):
        parser.error('missing Flutter assets')
except (KeyError, TypeError, ValueError) as exc:
    parser.error(f'invalid build manifest: {exc}')
windows = args.platform == 'windows-x64'
suffix = '.exe' if windows else ''
reset = 'reset-admin-password.cmd' if windows else 'reset-admin-password.sh'
required = ('rurimecha-core' + suffix, 'ruri-scrape' + suffix, 'rurimecha-api-docs.tar.gz')
for name in required:
    if not (args.core_package / name).is_file() or (args.core_package / name).is_symlink():
        parser.error(f'missing or invalid core artifact: {name}')
notices = args.core_package / 'third_party'
if not notices.is_dir() or not artifact_files(notices):
    parser.error('third-party license notices are required')
# Inspect the contract archive before redistributing it.
with tarfile.open(args.core_package / 'rurimecha-api-docs.tar.gz') as archive:
    for member in archive.getmembers():
        path = Path(member.name)
        if path.is_absolute() or '..' in path.parts or not path.parts or path.parts[0] != 'api' or not (member.isfile() or member.isdir()):
            parser.error(f'non-contract archive entry: {member.name}')
if args.apk and (not args.apk.is_file() or args.apk.is_symlink() or args.apk.suffix != '.apk'):
    parser.error('APK must be a regular .apk build artifact')
output.parent.mkdir(parents=True, exist_ok=True)
with tempfile.TemporaryDirectory(prefix='release-', dir=output.parent) as temporary:
    staging = Path(temporary)
    attachments = staging / 'attachments'
    attachments.mkdir()
    core = staging / 'rurimecha-core'
    core.mkdir()
    for name in required:
        shutil.copy2(args.core_package / name, core / name)
    shutil.copytree(notices, core / 'third_party')
    shutil.copy2(root / 'packaging' / args.platform / reset, core / reset)
    if windows:
        with zipfile.ZipFile(attachments / f'rurimecha-core-{args.platform}-{args.version}.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
            for path in sorted(core.rglob('*')):
                if path.is_file():
                    archive.write(path, path.relative_to(staging).as_posix())
    else:
        with tarfile.open(attachments / f'rurimecha-core-{args.platform}-{args.version}.tar.gz', 'w:gz') as archive:
            archive.add(core, arcname='rurimecha-core')
    with tarfile.open(attachments / f'rurimecha-web-{args.version}.tar.gz', 'w:gz') as archive:
        archive.add(args.web, arcname='rurimecha-web')
    if not windows:
        docker = root / 'packaging/docker'
        with tarfile.open(attachments / f'rurimecha-docker-{args.version}.tar.gz', 'w:gz') as archive:
            for name in ('compose.yaml', 'reset-admin-password.sh'):
                archive.add(docker / name, arcname=f'rurimecha-docker/{name}')
    if args.apk:
        shutil.copy2(args.apk, attachments / f'rurimecha-android-{args.version}.apk')
    metadata = {'version': args.version, 'platform': args.platform, 'artifacts': {p.name: digest(p) for p in sorted(attachments.iterdir())}}
    (attachments / 'release.json').write_text(json.dumps(metadata, indent=2) + '\n')
    sums = ''.join(f'{digest(p)}  {p.name}\n' for p in sorted(attachments.iterdir()))
    (attachments / 'SHA256SUMS').write_text(sums)
    attachments.rename(output)
print(output)
