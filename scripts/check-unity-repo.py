import json
import re
import subprocess
import sys
from pathlib import Path, PurePosixPath

GENERATED_FOLDERS = {
    "library", "temp", "obj", "logs", "usersettings", "build", "builds",
    "memorycaptures", "recordings", ".vs", ".idea",
}
REQUIRED_FILES = {
    "Packages/manifest.json",
    "Packages/packages-lock.json",
    "ProjectSettings/ProjectVersion.txt",
}


def check_assets(tracked):
    assets = {
        name for name in tracked
        if name.startswith("Assets/") and not any(
            part.startswith(".") or part.endswith("~") or part == "CVS"
            for part in PurePosixPath(name).parts[1:]
        )
    }
    metadata = {name for name in assets if name.endswith(".meta")}
    imports = assets - metadata
    folders = {
        parent.as_posix()
        for name in imports
        for parent in PurePosixPath(name).parents
        if parent.as_posix().startswith("Assets/")
    }
    expected = {f"{name}.meta" for name in imports | folders}
    errors = [f"Missing metadata: {name}" for name in sorted(expected - metadata)]
    # Git cannot track an empty directory. Its metadata alone can be valid.
    guids = {}
    for name in sorted(metadata):
        match = re.search(
            r"(?m)^guid: ([0-9a-fA-F]{32})[ \t]*$",
            Path(name).read_text(encoding="utf-8-sig"),
        )
        if not match:
            errors.append(f"Missing or invalid Unity GUID: {name}")
            continue
        guid = match[1].lower()
        if guid in guids:
            errors.append(f"Duplicate Unity GUID: {guids[guid]} and {name}")
        else:
            guids[guid] = name
    return errors


def check_project_settings():
    version = Path("ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8-sig")
    errors = []
    if not re.search(r"(?m)^m_EditorVersion: \d+\.\d+\.\d+[abfp]\d+[ \t]*$", version):
        errors.append("ProjectVersion.txt must pin a Unity Editor version.")

    manifest = json.loads(Path("Packages/manifest.json").read_text(encoding="utf-8-sig"))
    lock = json.loads(Path("Packages/packages-lock.json").read_text(encoding="utf-8-sig"))
    for name in sorted(manifest["dependencies"]):
        if lock["dependencies"].get(name, {}).get("depth") != 0:
            errors.append(f"Direct dependency missing from lockfile: {name}")
    return errors


def main():
    tracked = set(subprocess.check_output(["git", "ls-files", "-z"]).decode("utf-8").split("\0"))
    errors = [f"Required file is not tracked: {name}" for name in sorted(REQUIRED_FILES - tracked)]
    errors.extend(
        f"Generated folder is tracked: {name}"
        for name in sorted(tracked)
        if name.split("/", 1)[0].lower() in GENERATED_FOLDERS
    )
    try:
        errors.extend(check_assets(tracked))
        if REQUIRED_FILES <= tracked:
            errors.extend(check_project_settings())
    except (OSError, ValueError, KeyError, AttributeError, TypeError) as error:
        errors.append(f"Could not read project files: {error}")

    if errors:
        print("\n".join(errors), file=sys.stderr)
        return 1

    print("Unity repository checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
