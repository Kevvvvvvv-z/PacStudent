# PacStudent - Assessment 3

Editor: Unity 6000.4.11f1. Template: bundled Universal 2D.
Open Assets/Scenes/Level01.unity. The scene currently contains the template camera and global 2D light; its empty black view is intentional at the setup stage.

## Git workflow

Main is the release branch. Development holds completed work. Create each feature branch from the current Development branch when its phase starts: Feature-Audio, Feature-Visual, Feature-ManualLevel, Feature-Movement, Feature-LevelGenerator. Commit actual milestones, merge each completed feature back with a merge commit, retain its branch, and push it. Merge Development into Main and select Main before preparing the final ZIP.

The repository uses a local Git identity and dedicated SSH key for Kevvvvvvv-z. Credentials and private keys are outside the project and must never be committed.

## Current phase

Phase 1: required folders, Level01, orthographic camera, solid black background, text-serialized Unity assets, standard Unity.gitignore.

Sounds contains the supplied source audio and is not imported yet. Import and licensing review belong on Feature-Audio. Library, Logs, Temp and UserSettings are excluded by Unity.gitignore. Keep Unity .meta files with their corresponding assets.

## Authorship

See AI-ASSISTANCE.md. Student-authored graphics and scripts must follow the assessment's authorship requirements. No gameplay scripts or submission graphics have been generated during project setup.
