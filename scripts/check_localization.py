#!/usr/bin/env python3
"""
Localization Checker Script
Detects all projects in the source folder, extracts keys from their en.axaml files,
and checks which ones are used in code files.
Also detects hardcoded text in AXAML files that should be localized.
"""

import argparse
import logging
import re
import sys
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path

SOURCE_PATH = Path("source")
LANGUAGE_FILE_NAME = "en.axaml"
LANGUAGE_RELATIVE_DIR = Path("Resources") / "Language"
IGNORE_FOLDERS = {"Resources", "obj", "bin"}

DYNAMIC_RESOURCE_PATTERN = re.compile(r"\{DynamicResource\s+([^}]+)\}")
LOCALIZATION_HELPER_PATTERN = re.compile(
    r'LocalizationHelper\.GetText\(["\']([^"\']+)["\']\)'
)
KEY_PATTERN = re.compile(r'x:Key="([^"]+)"')
# Skip keys that don't contain a dot - these are typically theme resources (brushes, colors, styles)
# Localization keys always have a dot (e.g., "GameDetailsEditor.Background.Clear")
SKIP_PATTERN = re.compile(r"^[^.]+$")

# Pattern to detect hardcoded text in common text properties
# Matches: Text, Header, Title, Description, Content, Watermark, StatusText, ProgressText, ToolTip.Tip
HARDCODED_TEXT_PATTERN = re.compile(
    r"\b(Text|Header|Title|Description|Content|Watermark|StatusText|ProgressText|ToolTip\.Tip)"
    r'\s*=\s*"([^"]+)"',
    re.IGNORECASE,
)

# Pattern to detect hardcoded text in _messageBoxService method calls
# Matches: _messageBoxService.ShowInfoAsync("title", "message") or _messageBoxService.ShowInfoAsync("title", $"...")
MESSAGEBOX_SERVICE_PATTERN = re.compile(
    r'_messageBoxService\.(?:ShowInfoAsync|ShowWarningAsync|ShowErrorAsync|ShowConfirmationAsync|ShowCustomDialogAsync)\s*\(\s*"([^"]+)"(?:\s*,\s*(\$)?"([^"]+)")?',
    re.IGNORECASE,
)

# Patterns to skip - bindings, markup extensions, empty strings, design-time only, single symbols
SKIP_TEXT_PATTERN = re.compile(
    r"^(\{|x:|d:|\$|\*|<|>|/|\\|\[|\]|\(|\)|#|@|!|\?|\.|,|;|:|\+|-|_|=|%|&|\||\^|~|`)"
)

logger = logging.getLogger(__name__)


@dataclass
class ScanResults:
    found: dict[str, list[str]] = field(default_factory=lambda: defaultdict(list))
    missing: dict[str, list[tuple[str, int, str]]] = field(
        default_factory=lambda: defaultdict(list)
    )  # (file, line_number, line_content)
    hardcoded: list[tuple[str, str, str, int, str]] = field(
        default_factory=list
    )  # (file, property, text, line_number, line_content)

    def merge(self, other: "ScanResults") -> None:
        for key, paths in other.found.items():
            self.found[key].extend(paths)
        for key, paths in other.missing.items():
            self.missing[key].extend(paths)
        self.hardcoded.extend(other.hardcoded)


@dataclass
class ProjectInfo:
    """Represents a detected project with its source files and optional language file."""

    name: str
    path: Path
    language_file: Path | None
    axaml_files: list[Path] = field(default_factory=list)
    cs_files: list[Path] = field(default_factory=list)

    @property
    def has_localization(self) -> bool:
        return self.language_file is not None


def discover_projects(source_path: Path) -> list[ProjectInfo]:
    """
    Discover all projects under the source path.
    A project is identified by having a .csproj file.
    Projects may or may not have a Resources/Language/en.axaml file.
    """
    projects = []

    for csproj_file in source_path.rglob("*.csproj"):
        project_path = csproj_file.parent
        project_name = project_path.name

        # Skip nested project directories (e.g., obj/bin containing .csproj references)
        try:
            relative = project_path.relative_to(source_path)
        except ValueError:
            continue
        if IGNORE_FOLDERS.intersection(relative.parts):
            continue

        # Check for language file
        language_file = project_path / LANGUAGE_RELATIVE_DIR / LANGUAGE_FILE_NAME
        has_language_file = language_file.exists()

        # Collect source files
        axaml_files = [
            f
            for f in project_path.rglob("*.axaml")
            if (not has_language_file or f != language_file)
            and not should_ignore_file(f, project_path)
        ]
        cs_files = [
            f
            for f in project_path.rglob("*.cs")
            if not should_ignore_file(f, project_path)
        ]

        # Skip projects with no source files at all
        if not axaml_files and not cs_files:
            logger.debug("Skipping %s — no source files found", project_name)
            continue

        projects.append(
            ProjectInfo(
                name=project_name,
                path=project_path,
                language_file=language_file if has_language_file else None,
                axaml_files=axaml_files,
                cs_files=cs_files,
            )
        )

    projects.sort(key=lambda p: p.name)
    return projects


def extract_keys_from_en_axaml(file_path: Path) -> set[str]:
    """Extract all localization keys from en.axaml."""
    content = file_path.read_text(encoding="utf-8")
    keys = set(KEY_PATTERN.findall(content))
    logger.info("Found %d keys in %s", len(keys), file_path.name)
    return keys


def should_skip_key(key: str) -> bool:
    """Return True for non-text resources like brushes, colors, and styles."""
    return bool(SKIP_PATTERN.search(key))


def should_ignore_file(file_path: Path, project_path: Path) -> bool:
    """Return True if the file lives inside an ignored folder."""
    try:
        relative = file_path.relative_to(project_path)
    except ValueError:
        return True
    return bool(IGNORE_FOLDERS.intersection(relative.parts))


def scan_file(
    file_path: Path, all_keys: set[str], pattern: re.Pattern, base_path: Path
) -> ScanResults:
    """Scan a single file using the given pattern and classify matches."""
    logger.debug("Scanning %s...", file_path.name)
    results = ScanResults()

    try:
        content = file_path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        content = file_path.read_text(encoding="utf-8-sig")

    relative_path = str(file_path.relative_to(base_path))

    for match in pattern.finditer(content):
        key = match.group(1).strip()
        if should_skip_key(key):
            continue

        line_number = content[: match.start()].count("\n") + 1
        line_start = content.rfind("\n", 0, match.start()) + 1
        line_end = content.find("\n", match.end())
        if line_end == -1:
            line_end = len(content)
        line = content[line_start:line_end]

        if key in all_keys:
            results.found[key].append(relative_path)
        else:
            results.missing[key].append((relative_path, line_number, line.strip()))

    return results


def scan_hardcoded_text(file_path: Path, base_path: Path) -> ScanResults:
    """Scan AXAML file for hardcoded text that should be localized."""
    logger.debug("Checking %s for hardcoded text...", file_path.name)
    results = ScanResults()

    try:
        content = file_path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        content = file_path.read_text(encoding="utf-8-sig")

    relative_path = str(file_path.relative_to(base_path))

    # Check for attribute-based text (Text="...", Header="...", etc.)
    for match in HARDCODED_TEXT_PATTERN.finditer(content):
        property_name = match.group(1)
        text_value = match.group(2).strip()

        # Skip empty strings, bindings, markup extensions, single symbols, and single characters
        if (
            not text_value
            or len(text_value) <= 1
            or SKIP_TEXT_PATTERN.match(text_value)
        ):
            continue

        # Skip design-time properties (d: prefix in the line)
        line_start = content.rfind("\n", 0, match.start()) + 1
        line_end = content.find("\n", match.end())
        if line_end == -1:
            line_end = len(content)
        line = content[line_start:line_end]
        if "d:" in line or "x:" in line:
            continue

        line_number = content[: match.start()].count("\n") + 1
        results.hardcoded.append(
            (relative_path, property_name, text_value, line_number, line.strip())
        )

    # Check for plain text content in UserControl/Window tags
    plain_text_pattern = re.compile(
        r"<(?:UserControl|Window)[^>]*>\s*([A-Za-z][A-Za-z\s]{2,}?)\s*<",
        re.IGNORECASE,
    )
    for match in plain_text_pattern.finditer(content):
        text_value = match.group(1).strip()
        if text_value and not text_value.startswith("{") and len(text_value) > 2:
            line_number = content[: match.start()].count("\n") + 1
            line_start = content.rfind("\n", 0, match.start()) + 1
            line_end = content.find("\n", match.end())
            if line_end == -1:
                line_end = len(content)
            line = content[line_start:line_end]
            results.hardcoded.append(
                (relative_path, "Content", text_value, line_number, line.strip())
            )

    return results


def scan_messagebox_service(file_path: Path, base_path: Path) -> ScanResults:
    """Scan C# file for hardcoded text in _messageBoxService method calls."""
    logger.debug("Checking %s for _messageBoxService usage...", file_path.name)
    results = ScanResults()

    try:
        content = file_path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        content = file_path.read_text(encoding="utf-8-sig")

    relative_path = str(file_path.relative_to(base_path))

    for match in MESSAGEBOX_SERVICE_PATTERN.finditer(content):
        title = match.group(1).strip()
        is_interpolated = match.group(2) == "$"
        message = match.group(3).strip() if match.group(3) else ""

        # Skip if the text uses LocalizationHelper
        if "LocalizationHelper" in match.group(0):
            continue

        line_number = content[: match.start()].count("\n") + 1
        line_start = content.rfind("\n", 0, match.start()) + 1
        line_end = content.find("\n", match.end())
        if line_end == -1:
            line_end = len(content)
        line = content[line_start:line_end]

        # Skip empty strings or single characters
        if title and len(title) > 1 and not SKIP_TEXT_PATTERN.match(title):
            results.hardcoded.append(
                (relative_path, "Title", title, line_number, line.strip())
            )

        if message and len(message) > 1:
            if is_interpolated:
                results.hardcoded.append(
                    (
                        relative_path,
                        "Message (interpolated)",
                        message,
                        line_number,
                        line.strip(),
                    )
                )
            else:
                results.hardcoded.append(
                    (relative_path, "Message", message, line_number, line.strip())
                )

    return results


def scan_project(project: ProjectInfo, all_keys: set[str]) -> ScanResults:
    """Run all scans for a single project against the combined key set."""
    results = ScanResults()

    logger.info("Scanning AXAML files for DynamicResource usage...")
    for f in project.axaml_files:
        results.merge(scan_file(f, all_keys, DYNAMIC_RESOURCE_PATTERN, project.path))

    logger.info("Scanning C# files for LocalizationHelper.GetText() usage...")
    for f in project.cs_files:
        results.merge(scan_file(f, all_keys, LOCALIZATION_HELPER_PATTERN, project.path))

    logger.info("Checking AXAML files for hardcoded text...")
    for f in project.axaml_files:
        results.merge(scan_hardcoded_text(f, project.path))

    logger.info("Checking C# files for _messageBoxService usage...")
    for f in project.cs_files:
        results.merge(scan_messagebox_service(f, project.path))

    return results


def report_project(
    project: ProjectInfo,
    all_keys: set[str],
    project_keys: set[str],
    results: ScanResults,
) -> bool:
    """Print the report for a single project. Returns True if there are errors."""
    all_referenced = set(results.found) | set(results.missing)
    div = "=" * 60
    sub = "- " * 30

    print(f"\n{div}")
    print(f"PROJECT: {project.name}")

    if project.has_localization:
        unused_keys = project_keys - all_referenced
        print(f"Language file: {project.language_file}")
    else:
        unused_keys = set()
        print("Language file: None (class library — validated against all keys)")

    print(div)

    print(f"\n  Keys validated against:              {len(all_keys)}")

    if project.has_localization:
        print(f"  Keys defined in this project:        {len(project_keys)}")

    print(f"  Total unique keys referenced:        {len(all_referenced)}")
    print(f"  Keys found:                          {len(results.found)}")
    print(f"  Keys missing:                        {len(results.missing)}")
    print(f"  Hardcoded text detected:             {len(results.hardcoded)}")

    if project.has_localization:
        print(
            f"\n  {sub}\n"
            f"  UNUSED KEYS - defined in en.axaml but not referenced in code\n"
            f"  {sub}"
        )

        if unused_keys:
            print(f"\n  {len(unused_keys)} unused key(s):")
            for key in sorted(unused_keys):
                print(f"    {key}")
        else:
            print("\n  [OK] All keys in en.axaml are being used")

    print(
        f"\n  {sub}\n"
        f"  HARDCODED TEXT - text that may need localization\n"
        f"  {sub}"
    )

    if results.hardcoded:
        print(f"\n  {len(results.hardcoded)} instance(s) found:")
        for (
            file_path,
            property_name,
            text_value,
            line_number,
            line_content,
        ) in results.hardcoded:
            print(f"    {file_path}:{line_number}")
            print(f'      {property_name}="{text_value}"')
            print(f"      {line_content}")
        print("\n  [WARN] Review the above text for potential localization")
    else:
        print("\n  [OK] There are no hardcoded text properties")

    print(
        f"\n  {sub}\n"
        f"  MISSING KEYS - referenced in code but not defined in any en.axaml\n"
        f"  {sub}"
    )

    if results.missing:
        for key in sorted(results.missing):
            print(f"    {key}")
            for path, line_number, line_content in results.missing[key]:
                print(f"     - {path}:{line_number}")
                print(f"       {line_content}")
        return True

    print("\n  [OK] All keys referenced in code are defined in en.axaml")
    return False


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Check localization keys in en.axaml against code usage"
    )
    parser.add_argument("--debug", action="store_true", help="Enable debug logging")
    parser.add_argument(
        "--project",
        type=str,
        default=None,
        help="Only check a specific project by name (e.g., AnimusReforged.Altair)",
    )
    args = parser.parse_args()

    logging.basicConfig(
        level=logging.DEBUG if args.debug else logging.INFO,
        format="[%(levelname)s] %(message)s",
    )

    logger.info("Source path: %s", SOURCE_PATH.absolute())

    projects = discover_projects(SOURCE_PATH)

    if not projects:
        logger.error("No projects found in %s", SOURCE_PATH)
        sys.exit(1)

    if args.project:
        projects = [p for p in projects if p.name == args.project]
        if not projects:
            logger.error("Project '%s' not found", args.project)
            sys.exit(1)

    localized_projects = [p for p in projects if p.has_localization]
    library_projects = [p for p in projects if not p.has_localization]

    logger.info(
        "Discovered %d project(s): %s",
        len(projects),
        ", ".join(p.name for p in projects),
    )

    if localized_projects:
        logger.info("  Localized: %s", ", ".join(p.name for p in localized_projects))
    if library_projects:
        logger.info(
            "  Libraries (no en.axaml): %s",
            ", ".join(p.name for p in library_projects),
        )

    # Build a combined key set from all localized projects
    project_keys_map: dict[str, set[str]] = {}
    all_keys: set[str] = set()

    for project in localized_projects:
        assert project.language_file is not None
        keys = extract_keys_from_en_axaml(project.language_file)
        project_keys_map[project.name] = keys
        all_keys |= keys

    logger.info("Combined key set: %d unique keys across all projects", len(all_keys))

    # Scan all projects against the combined key set
    div = "=" * 60
    has_any_errors = False

    for project in projects:
        logger.info("")
        logger.info("Processing project: %s", project.name)
        logger.info(
            "Found %d .axaml files and %d .cs files to scan",
            len(project.axaml_files),
            len(project.cs_files),
        )

        results = scan_project(project, all_keys)
        project_keys = project_keys_map.get(project.name, set())
        has_errors = report_project(project, all_keys, project_keys, results)
        if has_errors:
            has_any_errors = True

    print(f"\n{div}")
    print(
        "RESULT: FAILED - Missing localization keys detected"
        if has_any_errors
        else "RESULT: PASSED - All localization keys are valid"
    )
    print(div)

    sys.exit(1 if has_any_errors else 0)


if __name__ == "__main__":
    main()
