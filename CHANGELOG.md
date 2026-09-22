# Changelog

## 1.2.2.3

### Improved

- Smoother gain movement in the OSD.
- Better visual feedback while volume is changing continuously.
- Improved fade and control animations.
- Lower overhead when using the Very Fast polling option.
- More reliable configuration reloads and shutdown handling.
- Safer self-updates and rollback behavior.
- Better fullscreen overlay cleanup when the OSD hides.

### Fixed

- Fixed an issue where the OSD could occasionally stop appearing until the application was restarted.
- Fixed Very Fast polling not actually running at 140 Hz.
- Fixed several window lifecycle issues that could leave stale overlay state behind.
- Fixed configuration watcher edge cases when files are replaced or renamed.
- Fixed updater problems with nested folders, failed installs and unsafe archive paths.

### Known issue

- NVIDIA RTX HDR may still temporarily drop in some games while the OSD is visible.

## 1.2.2.2

Base version from the original Voicemeeter Fancy OSD project.

