import subprocess
import uuid
from pathlib import Path

ROOT = Path(__file__).parents[2]


def test_package_contains_mod_service_and_instructions_without_sdk_or_weights():
    destination = ROOT / "artifacts/package-tests" / uuid.uuid4().hex
    result = subprocess.run(["powershell", "-NoProfile", "-File", str(ROOT / "scripts/package.ps1"), "-OutputDirectory", str(destination)], capture_output=True, text=True)
    assert result.returncode == 0, result.stdout + result.stderr
    assert (destination / "scripts/Pursuit.Legacy.dll").is_file()
    assert (destination / "scripts/Pursuit.Core.dll").is_file()
    assert (destination / "scripts/Pursuit.ini").is_file()
    assert (destination / "service/pursuit_service/model.py").is_file()
    assert (destination / "docs/setup.md").is_file()
    assert (destination / "config/runtime-manifest.json").is_file()
    assert (destination / "src/Pursuit.Legacy/PursuitScript.cs").is_file()
    assert (destination / "docs/THIRD_PARTY_NOTICES.md").is_file()
    assert not list(destination.rglob("*.safetensors"))
    assert not list(destination.rglob("ScriptHookVDotNet3.dll"))
    again = subprocess.run(["powershell", "-NoProfile", "-File", str(ROOT / "scripts/package.ps1"), "-OutputDirectory", str(destination)], capture_output=True, text=True)
    assert again.returncode != 0


def test_package_refuses_workspace_root():
    result = subprocess.run(["powershell", "-NoProfile", "-File", str(ROOT / "scripts/package.ps1"), "-OutputDirectory", str(ROOT)], capture_output=True, text=True)
    assert result.returncode != 0
