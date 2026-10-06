#!/usr/bin/env bash
# Creates the agreed Assets/_Project folder structure inside the Unity project.
# Run from the repository root (the folder that contains Assets/, Packages/, ProjectSettings/).
set -euo pipefail

if [ ! -d "Assets" ] || [ ! -d "ProjectSettings" ]; then
  echo "Error: run this from the Unity project root (Assets/ and ProjectSettings/ not found)." >&2
  exit 1
fi

folders=(
  "Assets/_Project/Scenes/Sandbox"
  "Assets/_Project/Scripts/Core"
  "Assets/_Project/Scripts/Player"
  "Assets/_Project/Scripts/Interaction"
  "Assets/_Project/Scripts/AI"
  "Assets/_Project/Scripts/AgentBody"
  "Assets/_Project/Models/Source"
  "Assets/_Project/Models/Export"
  "Assets/_Project/Materials"
  "Assets/_Project/Textures"
  "Assets/_Project/Lighting"
  "Assets/_Project/Prefabs"
  "Assets/_Project/Animations"
  "Assets/_Project/Audio"
  "Assets/_Project/UI"
  "Assets/_Project/ThirdParty"
  "Assets/_Project/Tests/EditMode"
  "docs/evidence"
)

for f in "${folders[@]}"; do
  mkdir -p "$f"
  # .gitkeep lets Git track the empty folder (Unity ignores dot-files)
  [ -z "$(ls -A "$f")" ] && touch "$f/.gitkeep"
  echo "created $f"
done

echo
echo "Done. Open the project in Unity once so it generates .meta files for the new folders, then commit."
