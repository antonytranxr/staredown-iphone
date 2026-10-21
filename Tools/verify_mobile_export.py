#!/usr/bin/env python3
"""Read-only validation of the Staredown 1.1.9 (12) Unity iPhone export.

Usage: python3 verify_export.py EXPORT [--source-project UNITY_PROJECT]
Uses Python's standard library and macOS plutil to read Xcode's OpenStep plist.
Nothing is written to the export or source project. JSON goes only to stdout.
"""
import argparse
import hashlib
import json
from pathlib import Path
import plistlib
import re
import shlex
import subprocess
import sys


VERSION, BUILD = "1.1.9", "12"
ORIENTATIONS = {
    "UIInterfaceOrientationPortrait",
    "UIInterfaceOrientationLandscapeLeft",
    "UIInterfaceOrientationLandscapeRight",
}
SYMBOLS = {
    "orientation": "StareMobileIOSRefreshOrientation",
    "drain_samples": "StareMobileIOSDrainSamples",
    "latest_capture_timestamp": "StareMobileIOSLatestCaptureTimestamp",
}


def verify(export, source_project):
    checks = []
    details = {"export": str(export), "version": None, "build": None}

    def check(name, passed, detail=None):
        checks.append((name, bool(passed), detail))

    def read_text(relative):
        path = export / relative
        if not path.is_file():
            return ""
        return path.read_text(encoding="utf-8", errors="replace")

    check("export_directory", export.is_dir())
    info = {}
    try:
        with (export / "Info.plist").open("rb") as stream:
            info = plistlib.load(stream)
        check("info_plist", isinstance(info, dict))
    except (OSError, ValueError, plistlib.InvalidFileException) as error:
        check("info_plist", False, str(error))
    details.update(version=info.get("CFBundleShortVersionString"), build=info.get("CFBundleVersion"))
    check("app_version", details["version"] == VERSION, f"expected {VERSION}, got {details['version']}")
    check("app_build", str(details["build"]) == BUILD, f"expected {BUILD}, got {details['build']}")
    check("portrait_default", info.get("UIInterfaceOrientation") == "UIInterfaceOrientationPortrait")
    for key in ("UISupportedInterfaceOrientations", "UISupportedInterfaceOrientations~ipad"):
        values = info.get(key)
        check(key, isinstance(values, list) and len(values) == 3 and set(values) == ORIENTATIONS,
              f"expected portrait and both landscapes only, got {values}")
    manifest = info.get("UIApplicationSceneManifest", {})
    configurations = manifest.get("UISceneConfigurations", {}) if isinstance(manifest, dict) else {}
    application = configurations.get("UIWindowSceneSessionRoleApplication", []) if isinstance(configurations, dict) else []
    check("scene_manifest_UnityScene", isinstance(application, list) and any(
        isinstance(item, dict) and item.get("UISceneDelegateClassName") == "UnityScene" for item in application))

    objects = {}
    pbx = export / "Unity-iPhone.xcodeproj/project.pbxproj"
    try:
        result = subprocess.run(["/usr/bin/plutil", "-convert", "json", "-o", "-", str(pbx)],
                                check=True, capture_output=True, text=True)
        project = json.loads(result.stdout)
        objects = project.get("objects", {})
        check("pbx_project", isinstance(objects, dict) and bool(objects))
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        check("pbx_project", False, getattr(error, "stderr", None) or str(error))

    framework_targets = [item for item in objects.values() if isinstance(item, dict) and
                         item.get("isa") == "PBXNativeTarget" and item.get("name") == "UnityFramework"]
    check("UnityFramework_target", len(framework_targets) == 1)
    phase_ids = framework_targets[0].get("buildPhases", []) if len(framework_targets) == 1 else []

    def phase_build_files(kind):
        return [objects.get(identifier, {}) for phase_id in phase_ids
                for phase in [objects.get(phase_id, {})] if phase.get("isa") == kind
                for identifier in phase.get("files", [])]

    sources = phase_build_files("PBXSourcesBuildPhase")
    frameworks = phase_build_files("PBXFrameworksBuildPhase")

    def file_references(relative):
        return {identifier for identifier, item in objects.items() if isinstance(item, dict) and
                item.get("isa") == "PBXFileReference" and item.get("path") == relative}

    def source_entries(relative):
        references = file_references(relative)
        return [entry for entry in sources if entry.get("fileRef") in references]

    for filename in ("UnityScene.h", "UnityScene.mm"):
        relative = "Classes/UI/" + filename
        text = read_text(relative)
        check(filename + "_native_file", bool(text))
        check(filename + "_pbx_reference", bool(file_references(relative)))
        required = "UIWindowSceneDelegate" if filename.endswith(".h") else "@implementation UnityScene"
        check(filename + "_implementation", required in text)
    check("UnityScene_in_UnityFramework_Sources", bool(source_entries("Classes/UI/UnityScene.mm")))
    for filename in ("StaredownMobileCamera.mm", "StaredownAudioRoute.mm"):
        relative = "Libraries/Plugins/iOS/" + filename
        check(filename + "_exported", (export / relative).is_file())
        entries = source_entries(relative)
        check(filename + "_in_UnityFramework_Sources", bool(entries))
        arc = bool(entries) and all("-fobjc-arc" in shlex.split(str(
            entry.get("settings", {}).get("COMPILER_FLAGS", ""))) for entry in entries)
        check(filename + "_ARC", arc)
    for name in ("ARKit.framework", "AVFoundation.framework"):
        linked = [objects.get(entry.get("fileRef"), {}) for entry in frameworks]
        check(name + "_linked_to_UnityFramework", any(
            Path(str(item.get("path", ""))).name == name for item in linked))

    native_relative = "Libraries/Plugins/iOS/StaredownMobileCamera.mm"
    source = source_project / "Assets/Plugins/iOS/StaredownMobileCamera.mm"
    exported = export / native_relative
    try:
        source_bytes, exported_bytes = source.read_bytes(), exported.read_bytes()
        details["nativeCameraSha256"] = hashlib.sha256(exported_bytes).hexdigest()
        details["expectedNativeCameraSha256"] = hashlib.sha256(source_bytes).hexdigest()
        check("native_camera_exact_source_bytes", source_bytes == exported_bytes)
    except OSError as error:
        check("native_camera_exact_source_bytes", False, str(error))

    generated = export / "Il2CppOutputProject/Source/il2cppOutput"
    check("generated_IL2CPP_directory", generated.is_dir())
    symbol_files = {label: [] for label in SYMBOLS}
    direct_calls = {label: False for label in SYMBOLS}
    # The phone camera is in Assembly-CSharp. A native definition alone does
    # not prove that IL2CPP retained the managed P/Invoke and actual call site.
    for path in sorted(generated.glob("Assembly-CSharp*.cpp")):
        text = path.read_text(encoding="utf-8", errors="replace")
        for label, symbol in SYMBOLS.items():
            if symbol in text:
                symbol_files[label].append(str(path.relative_to(export)))
                direct_calls[label] |= bool(re.search(
                    r"reinterpret_cast\s*<\s*PInvokeFunc\s*>\s*\(\s*" + re.escape(symbol) + r"\s*\)", text))
    details["nativeSymbolFiles"] = symbol_files
    for label, symbol in SYMBOLS.items():
        check("generated_IL2CPP_" + label + "_symbol", bool(symbol_files[label]), symbol)
        check("generated_IL2CPP_" + label + "_PInvoke_call", direct_calls[label], symbol)
    failed = [{"check": name, **({"detail": detail} if detail else {})}
              for name, passed, detail in checks if not passed]
    return {"ok": not failed, "checks": len(checks), "passed": len(checks) - len(failed),
            "failed": failed, **details}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("export", type=Path)
    parser.add_argument("--source-project", type=Path,
                        default=Path(__file__).resolve().parents[4] / "MetaVRStarter")
    arguments = parser.parse_args()
    try:
        report = verify(arguments.export.expanduser().resolve(), arguments.source_project.expanduser().resolve())
    except Exception as error:
        report = {"ok": False, "failed": [{"check": "validator_execution", "detail": str(error)}]}
    print(json.dumps(report, separators=(",", ":"), ensure_ascii=False))
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
